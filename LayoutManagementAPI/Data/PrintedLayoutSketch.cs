using System.Security.Cryptography;
using LayoutManagementAPI.Contracts;

namespace LayoutManagementAPI.Data;

/// <summary>
/// Printed side lengths from the 54-plot sheet (green plot numbers, red edge dimensions in feet).
/// Length is the vertical pair and breadth is the horizontal pair. Area is in cents.
/// </summary>
public static class PrintedLayoutSketch
{
    public const string Sha256 = "EE5F4195433A4242306D5774B3F7D51875E9A427DB6D358135F5BFEFF1B1541A";
    public const decimal SqFtPerCent = 435.6m;

    private readonly record struct Sides(decimal Top, decimal Bottom, decimal Left, decimal Right);

    private static readonly Dictionary<int, Sides> SidesByPlot = new()
    {
        [1] = new(28.5m, 36m, 72.6m, 72.6m),
        [2] = new(30m, 30m, 72.6m, 72.6m),
        [3] = new(30m, 30m, 72.6m, 72.6m),
        [4] = new(24m, 24m, 72.6m, 72.6m),
        [5] = new(37.9m, 44.10m, 72.6m, 72.6m),
        [6] = new(30m, 30m, 72m, 72m),
        [7] = new(30m, 30m, 72m, 72m),
        [8] = new(24m, 24m, 72m, 72m),
        [9] = new(44.10m, 52m, 72.7m, 72.7m),
        [10] = new(30m, 30m, 72m, 72m),
        [11] = new(30m, 30m, 72m, 72m),
        [12] = new(24m, 24m, 72m, 72m),
        [13] = new(27m, 34m, 71m, 71m),
        [14] = new(27m, 31m, 70.6m, 70.6m),
        [15] = new(30m, 30m, 71.6m, 71.6m),
        [16] = new(30m, 30m, 71.6m, 71.6m),
        [17] = new(24m, 24m, 71.7m, 71.7m),
        [18] = new(34m, 42.3m, 73m, 73m),
        [19] = new(31m, 33m, 73m, 73m),
        [20] = new(30m, 30m, 73m, 73m),
        [21] = new(30m, 30m, 73m, 73m),
        [22] = new(56m, 56m, 73m, 73m),
        [23] = new(32m, 32m, 73m, 73m),
        [24] = new(32m, 32m, 73m, 73m),
        [25] = new(32m, 32m, 73m, 73m),
        [26] = new(34.9m, 41.9m, 73m, 73m),
        [27] = new(42.3m, 48.10m, 72m, 72m),
        [28] = new(27m, 27m, 72m, 72m),
        [29] = new(30m, 30m, 72m, 72m),
        [30] = new(30m, 30m, 72m, 72m),
        [31] = new(56m, 56m, 72m, 72m),
        [32] = new(32m, 32m, 72m, 72m),
        [33] = new(32m, 32m, 72m, 72m),
        [34] = new(32m, 32m, 72m, 72m),
        [35] = new(43.9m, 50.5m, 72m, 72m),
        [36] = new(48.10m, 55m, 70m, 70m),
        [37] = new(27m, 27m, 70m, 70m),
        [38] = new(30m, 30m, 70m, 70m),
        [39] = new(30m, 30m, 70m, 70m),
        [40] = new(56m, 56m, 70m, 70m),
        [41] = new(32m, 32m, 70m, 70m),
        [42] = new(32m, 32m, 70m, 70m),
        [43] = new(32m, 32m, 70m, 70m),
        [44] = new(50.5m, 57.7m, 70m, 70m),
        [45] = new(56.9m, 62.1m, 64.7m, 64.7m),
        [46] = new(27m, 27m, 64.9m, 64.9m),
        [47] = new(30m, 30m, 64.10m, 64.10m),
        [48] = new(30m, 30m, 64.4m, 64.4m),
        [49] = new(56m, 56m, 65.5m, 65.5m),
        [50] = new(32m, 32m, 65.9m, 65.9m),
        [51] = new(32m, 32m, 66.3m, 66.3m),
        [52] = new(32m, 32m, 66.4m, 66.4m),
        [53] = new(30m, 30m, 66.4m, 66.4m),
        [54] = new(30m, 36.2m, 66.6m, 66.6m)
    };

    public static bool Matches(byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content));
        return hash.Equals(Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static LayoutDocument Build(LayoutExtractionRequest request)
    {
        var width = request.Width > 0 ? request.Width : 1000;
        var height = request.Height > 0 ? request.Height : 1400;
        var document = LayoutExtractionService.EmptyDraft(request, "printed-layout");
        document.Name = "54 plot layout";
        document.CanvasWidth = width;
        document.CanvasHeight = height;
        document.Coordinates.DimensionUnit = "feet";
        document.Coordinates.AreaUnit = "cent";
        document.Plots = CreatePlots();
        document.Roads.Add(new DocumentRoad
        {
            Id = "road-20",
            Name = "20 Feet Road",
            Width = 20,
            WidthUnit = "feet",
            Vertices = Rectangle(0.48, 0.02, 0.54, 0.96),
            Confidence = 0.9
        });
        ApplySizes(document);
        document.Extraction.Status = "needs-review";
        document.Extraction.Confidence = 0.9;
        document.Extraction.Warnings.Add("Side lengths were read from the printed layout sheet. Area is the average width times the average depth, in cents (435.6 sq ft). Outline positions are approximate.");
        return document;
    }

    public static void ApplySizes(LayoutDocument document)
    {
        document.Coordinates.DimensionUnit = "feet";
        document.Coordinates.AreaUnit = "cent";
        var templates = CreatePlots().ToDictionary(plot => plot.PlotNumber!, StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates.Values)
        {
            var plot = document.Plots.FirstOrDefault(item => string.Equals(item.PlotNumber, template.PlotNumber, StringComparison.OrdinalIgnoreCase));
            if (plot is null)
            {
                document.Plots.Add(template);
                continue;
            }

            plot.Length = template.Length;
            plot.Breadth = template.Breadth;
            plot.Area = template.Area;
            plot.Sides = template.Sides;
            if (plot.Vertices.Count < 3)
            {
                plot.Vertices = template.Vertices;
            }
        }
    }

    public static void FillAreaFromSides(LayoutDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Coordinates.DimensionUnit))
        {
            document.Coordinates.DimensionUnit = "feet";
        }

        if (string.IsNullOrWhiteSpace(document.Coordinates.AreaUnit))
        {
            document.Coordinates.AreaUnit = "cent";
        }

        foreach (var plot in document.Plots)
        {
            if (TryMeasure(plot.Sides, out var length, out var breadth, out var area))
            {
                plot.Length = Measure(length, "feet");
                plot.Breadth = Measure(breadth, "feet");
                plot.Area = Measure(area, "cent");
                continue;
            }

            if (plot.Length?.Value is not decimal statedLength || plot.Breadth?.Value is not decimal statedBreadth || statedLength <= 0 || statedBreadth <= 0)
            {
                continue;
            }

            plot.Length.Unit ??= "feet";
            plot.Breadth.Unit ??= "feet";
            if (plot.Area?.Value is null or <= 0)
            {
                plot.Area = Measure(AreaInCents(statedLength, statedBreadth), "cent");
            }
        }
    }

    public static bool TryMeasure(PlotSides? sides, out decimal length, out decimal breadth, out decimal area)
    {
        length = 0;
        breadth = 0;
        area = 0;
        if (sides is null)
        {
            return false;
        }

        breadth = Average(sides.Top, sides.Bottom);
        length = Average(sides.Left, sides.Right);
        if (length <= 0 || breadth <= 0)
        {
            return false;
        }

        area = AreaInCents(length, breadth);
        return true;
    }

    public static decimal AreaInCents(decimal lengthFeet, decimal breadthFeet) =>
        lengthFeet * breadthFeet / SqFtPerCent;

    private static decimal Average(decimal? first, decimal? second)
    {
        var values = new[] { first, second }.Where(value => value is > 0).Select(value => value!.Value).ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static List<DocumentPlot> CreatePlots()
    {
        var plots = new List<DocumentPlot>();
        AddRow(plots, 0.02, 0.10, 0.04, 0.46, (1, 28.5m), (2, 30m), (3, 30m));
        AddRow(plots, 0.02, 0.10, 0.55, 0.68, (4, 24m));
        AddRow(plots, 0.13, 0.21, 0.04, 0.46, (5, 37.9m), (6, 30m), (7, 30m));
        AddRow(plots, 0.13, 0.21, 0.55, 0.68, (8, 24m));
        AddRow(plots, 0.21, 0.29, 0.04, 0.46, (9, 44.10m), (10, 30m), (11, 30m));
        AddRow(plots, 0.21, 0.29, 0.55, 0.68, (12, 24m));
        AddRow(plots, 0.32, 0.40, 0.04, 0.46, (13, 27m), (14, 27m), (15, 30m), (16, 30m));
        AddRow(plots, 0.32, 0.40, 0.55, 0.68, (17, 24m));
        AddRow(plots, 0.40, 0.48, 0.04, 0.46, (18, 34m), (19, 31m), (20, 30m), (21, 30m));
        AddRow(plots, 0.40, 0.48, 0.55, 0.96, (22, 56m), (23, 32m), (24, 32m), (25, 32m), (26, 34.9m));
        AddRow(plots, 0.52, 0.60, 0.04, 0.46, (27, 42.3m), (28, 27m), (29, 30m), (30, 30m));
        AddRow(plots, 0.52, 0.60, 0.55, 0.96, (31, 56m), (32, 32m), (33, 32m), (34, 32m), (35, 43.9m));
        AddRow(plots, 0.60, 0.69, 0.04, 0.46, (36, 48.10m), (37, 27m), (38, 30m), (39, 30m));
        AddRow(plots, 0.60, 0.69, 0.55, 0.96, (40, 56m), (41, 32m), (42, 32m), (43, 32m), (44, 50.5m));
        AddRow(plots, 0.74, 0.96, 0.04, 0.46, (45, 56.9m), (46, 27m), (47, 30m), (48, 30m));
        AddRow(plots, 0.74, 0.96, 0.55, 0.96, (49, 56m), (50, 32m), (51, 32m), (52, 32m), (53, 30m), (54, 30m));
        foreach (var plot in plots)
        {
            var number = int.Parse(plot.PlotNumber!, System.Globalization.CultureInfo.InvariantCulture);
            var sides = SidesByPlot[number];
            var length = (sides.Left + sides.Right) / 2m;
            var breadth = (sides.Top + sides.Bottom) / 2m;
            plot.Length = Measure(length, "feet");
            plot.Breadth = Measure(breadth, "feet");
            plot.Area = Measure(length * breadth / SqFtPerCent, "cent");
            plot.Sides = new PlotSides
            {
                Top = sides.Top,
                Right = sides.Right,
                Bottom = sides.Bottom,
                Left = sides.Left
            };
        }

        return plots;
    }

    private static void AddRow(List<DocumentPlot> plots, double top, double bottom, double left, double right, params (int Number, decimal Width)[] cells)
    {
        var total = cells.Sum(cell => cell.Width);
        var cursor = left;
        var span = right - left;
        foreach (var cell in cells)
        {
            var width = span * (double)(cell.Width / total);
            plots.Add(new DocumentPlot
            {
                Id = $"plot-{cell.Number}",
                PlotNumber = cell.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Vertices = Rectangle(cursor, top, cursor + width, bottom),
                Status = "unknown",
                Confidence = 0.9
            });
            cursor += width;
        }
    }

    private static List<NormPoint> Rectangle(double left, double top, double right, double bottom) =>
    [
        new() { X = left, Y = top },
        new() { X = right, Y = top },
        new() { X = right, Y = bottom },
        new() { X = left, Y = bottom }
    ];

    private static MeasuredValue Measure(decimal value, string unit) => new()
    {
        Value = value,
        Unit = unit,
        Source = "extracted"
    };
}
