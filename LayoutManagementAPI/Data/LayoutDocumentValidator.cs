using LayoutManagementAPI.Contracts;

namespace LayoutManagementAPI.Data;

public static class LayoutDocumentValidator
{
    public const string SchemaVersion = "2.0";
    private static readonly string[] Statuses = ["available", "blocked", "sold", "reserved", "unknown"];

    public static LayoutValidationReport Validate(LayoutDocument document)
    {
        var report = new LayoutValidationReport();
        if (!string.Equals(document.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            report.Errors.Add($"schemaVersion must be {SchemaVersion}.");
        }

        if (document.CanvasWidth <= 0 || document.CanvasHeight <= 0)
        {
            report.Errors.Add("Canvas width and height must be positive.");
        }

        if (!string.Equals(document.Coordinates.System, "normalized-image", StringComparison.Ordinal))
        {
            report.Warnings.Add("Coordinates are not marked as normalized image coordinates.");
        }

        var plotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plotNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < document.Plots.Count; i++)
        {
            var plot = document.Plots[i];
            var label = string.IsNullOrWhiteSpace(plot.PlotNumber) ? $"plots[{i}]" : $"plot {plot.PlotNumber}";
            if (string.IsNullOrWhiteSpace(plot.Id) || !plotIds.Add(plot.Id))
            {
                report.Errors.Add($"{label} is missing a unique id.");
            }

            if (string.IsNullOrWhiteSpace(plot.PlotNumber))
            {
                report.Warnings.Add($"{label} has no plot number.");
            }
            else if (!plotNumbers.Add(plot.PlotNumber.Trim()))
            {
                report.Errors.Add($"Plot number {plot.PlotNumber} is duplicated.");
            }

            if (!Statuses.Contains(plot.Status))
            {
                report.Errors.Add($"{label} has an unknown status.");
            }

            ValidateRing(plot.Vertices, label, report);
            ValidateMeasurement(plot.Area, $"{label} area", report);
            ValidateMeasurement(plot.Length, $"{label} length", report);
            ValidateMeasurement(plot.Breadth, $"{label} breadth", report);
            CompareShape(plot, label, report);
        }

        var roadIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var road in document.Roads)
        {
            var label = string.IsNullOrWhiteSpace(road.Name) ? "road" : $"road {road.Name}";
            if (string.IsNullOrWhiteSpace(road.Id) || !roadIds.Add(road.Id))
            {
                report.Errors.Add($"{label} is missing a unique id.");
            }

            if (road.Width is < 0)
            {
                report.Errors.Add($"{label} width cannot be negative.");
            }

            ValidateRing(road.Vertices, label, report);
            if (road.Vertices.Count >= 3)
            {
                foreach (var plot in document.Plots.Where(p => p.Vertices.Count >= 3 && RingsOverlap(p.Vertices, road.Vertices)))
                {
                    report.Warnings.Add($"{label} overlaps plot {plot.PlotNumber ?? plot.Id}.");
                }
            }
        }

        for (var i = 0; i < document.Plots.Count; i++)
        {
            for (var j = i + 1; j < document.Plots.Count; j++)
            {
                var left = document.Plots[i];
                var right = document.Plots[j];
                if (left.Vertices.Count >= 3 && right.Vertices.Count >= 3 && RingsOverlap(left.Vertices, right.Vertices))
                {
                    report.Warnings.Add($"Plot {left.PlotNumber ?? left.Id} overlaps plot {right.PlotNumber ?? right.Id}.");
                }
            }
        }

        if (document.Extraction.Confidence is < 0 or > 1)
        {
            report.Warnings.Add("Extraction confidence must be between 0 and 1.");
        }

        return report;
    }

    public static (double X, double Y) ToCanvas(NormPoint point, int width, int height) =>
        (point.X * width, point.Y * height);

    private static void ValidateRing(List<NormPoint> vertices, string label, LayoutValidationReport report)
    {
        if (vertices.Count < 3)
        {
            report.Errors.Add($"{label} needs at least 3 vertices.");
            return;
        }

        if (vertices.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1 || double.IsNaN(point.X) || double.IsNaN(point.Y)))
        {
            report.Errors.Add($"{label} has coordinates outside the 0 to 1 image canvas.");
        }

        if (SelfIntersects(vertices))
        {
            report.Warnings.Add($"{label} polygon appears self-intersecting.");
        }
    }

    private static void ValidateMeasurement(MeasuredValue? measurement, string label, LayoutValidationReport report)
    {
        if (measurement?.Value is < 0)
        {
            report.Errors.Add($"{label} cannot be negative.");
        }
    }

    private static void CompareShape(DocumentPlot plot, string label, LayoutValidationReport report)
    {
        if (plot.Area?.Value is null)
        {
            return;
        }

        if (plot.Length?.Value is null || plot.Breadth?.Value is null || plot.Vertices.Count < 3)
        {
            report.Warnings.Add($"{label} area cannot be checked against the outline because the image has no surveyed scale.");
            return;
        }

        var bounds = Bounds(plot.Vertices);
        var pixelRatio = bounds.Width / Math.Max(bounds.Height, 0.0001);
        var statedRatio = (double)plot.Breadth.Value.Value / Math.Max((double)plot.Length.Value.Value, 0.0001);
        var larger = Math.Max(pixelRatio, statedRatio);
        var smaller = Math.Max(Math.Min(pixelRatio, statedRatio), 0.0001);
        if (larger / smaller > 1.2)
        {
            report.Warnings.Add($"{label} outline proportions differ from the stated length and breadth.");
        }
    }

    private static bool RingsOverlap(List<NormPoint> left, List<NormPoint> right)
    {
        var a = Bounds(left);
        var b = Bounds(right);
        if (a.MaxX < b.MinX || b.MaxX < a.MinX || a.MaxY < b.MinY || b.MaxY < a.MinY)
        {
            return false;
        }

        return left.Any(point => Contains(right, point)) || right.Any(point => Contains(left, point));
    }

    private static bool Contains(List<NormPoint> ring, NormPoint point)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var yi = ring[i].Y;
            var yj = ring[j].Y;
            var xi = ring[i].X;
            var xj = ring[j].X;
            var intersect = (yi > point.Y) != (yj > point.Y)
                && point.X < (xj - xi) * (point.Y - yi) / ((yj - yi) == 0 ? 0.0000001 : (yj - yi)) + xi;
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool SelfIntersects(List<NormPoint> vertices)
    {
        var count = vertices.Count;
        for (var i = 0; i < count; i++)
        {
            var a1 = vertices[i];
            var a2 = vertices[(i + 1) % count];
            for (var j = i + 1; j < count; j++)
            {
                var adjacent = j == i || (i + 1) % count == j || (j + 1) % count == i;
                if (adjacent)
                {
                    continue;
                }

                var b1 = vertices[j];
                var b2 = vertices[(j + 1) % count];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(NormPoint a, NormPoint b, NormPoint c, NormPoint d)
    {
        var o1 = Orientation(a, b, c);
        var o2 = Orientation(a, b, d);
        var o3 = Orientation(c, d, a);
        var o4 = Orientation(c, d, b);
        return o1 != o2 && o3 != o4;
    }

    private static int Orientation(NormPoint a, NormPoint b, NormPoint c)
    {
        var value = (b.Y - a.Y) * (c.X - b.X) - (b.X - a.X) * (c.Y - b.Y);
        return value switch
        {
            > 0.0000001 => 1,
            < -0.0000001 => -1,
            _ => 0
        };
    }

    private static (double MinX, double MinY, double MaxX, double MaxY, double Width, double Height) Bounds(List<NormPoint> points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return (minX, minY, maxX, maxY, maxX - minX, maxY - minY);
    }
}
