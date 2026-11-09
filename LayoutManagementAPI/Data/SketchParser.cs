using System.Text.Json;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Data;

public static class SketchParser
{
    public const string SupportedVersion = "1.0";
    public const decimal SquareFeetPerCent = 435.6m;
    private static readonly string[] Units = ["sqyd", "sqft", "sqm", "cent"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(string json, string fileName, string uploadedBy, out LayoutPlan plan, out List<string> errors)
    {
        plan = new LayoutPlan();
        errors = new List<string>();
        SketchDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SketchDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"The file is not valid JSON. {ex.Message}");
            return false;
        }

        if (document is null)
        {
            errors.Add("The sketch file is empty.");
            return false;
        }

        if (!string.Equals(document.SchemaVersion, SupportedVersion, StringComparison.Ordinal))
        {
            errors.Add($"schemaVersion must be \"{SupportedVersion}\".");
        }

        if (string.IsNullOrWhiteSpace(document.LayoutName))
        {
            errors.Add("layoutName is required.");
        }

        var unit = document.Unit.Trim().ToLowerInvariant();
        if (!Units.Contains(unit))
        {
            errors.Add("unit must be cent, sqyd, sqft, or sqm.");
        }

        if (document.Plots.Count == 0)
        {
            errors.Add("plots must contain at least one plot.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plots = new List<Plot>();
        for (var index = 0; index < document.Plots.Count; index++)
        {
            var source = document.Plots[index];
            var label = string.IsNullOrWhiteSpace(source.PlotNumber) ? $"plots[{index}]" : source.PlotNumber.Trim();
            if (string.IsNullOrWhiteSpace(source.PlotNumber))
            {
                errors.Add($"{label}: plotNumber is required.");
            }
            else if (!seen.Add(source.PlotNumber.Trim()))
            {
                errors.Add($"{label}: plotNumber is duplicated.");
            }

            if (source.Area <= 0)
            {
                errors.Add($"{label}: area must be greater than zero.");
            }

            if (source.Vertices.Count < 3)
            {
                errors.Add($"{label}: vertices must contain at least 3 points so the outline can be measured.");
            }
            else
            {
                var measured = Shoelace(source.Vertices);
                if (unit == "cent")
                {
                    measured = Math.Round(measured / SquareFeetPerCent, 2, MidpointRounding.AwayFromZero);
                }

                var difference = Math.Abs(measured - source.Area);
                var tolerance = Math.Max(0.5m, source.Area * 0.01m);
                if (difference > tolerance)
                {
                    errors.Add($"{label}: declared area {source.Area} does not match the outline area {measured:0.##}. Check the vertices.");
                }
            }

            if (source.Price is < 0)
            {
                errors.Add($"{label}: price cannot be negative.");
            }

            plots.Add(new Plot
            {
                PlotNumber = source.PlotNumber.Trim(),
                Area = source.Area,
                LengthFeet = source.LengthFeet ?? 0,
                BreadthFeet = source.BreadthFeet ?? 0,
                Facing = string.IsNullOrWhiteSpace(source.Facing) ? null : source.Facing.Trim(),
                Price = source.Price ?? 0,
                Status = PlotStatus.Available,
                VerticesJson = JsonSerializer.Serialize(source.Vertices, JsonOptions)
            });
        }

        if (errors.Count > 0)
        {
            return false;
        }

        plan = new LayoutPlan
        {
            Name = document.LayoutName.Trim(),
            Unit = unit,
            Notes = string.IsNullOrWhiteSpace(document.Notes) ? null : document.Notes.Trim(),
            FileName = fileName,
            UploadedBy = uploadedBy,
            UploadedOn = DateTime.UtcNow,
            Plots = plots
        };
        return true;
    }

    public static decimal Shoelace(IReadOnlyList<SketchPoint> points)
    {
        decimal sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            sum += points[i].X * next.Y - next.X * points[i].Y;
        }

        return Math.Round(Math.Abs(sum) / 2m, 2, MidpointRounding.AwayFromZero);
    }

    public static List<SketchPoint> ReadVertices(string json)
    {
        return JsonSerializer.Deserialize<List<SketchPoint>>(json, JsonOptions) ?? new();
    }
}
