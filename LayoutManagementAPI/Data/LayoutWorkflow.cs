using System.Text.Json;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Data;

public class LayoutWorkflow
{
    private readonly RealEstateDbContext _db;
    private readonly ILayoutExtractionService _extraction;

    public LayoutWorkflow(RealEstateDbContext db, ILayoutExtractionService extraction)
    {
        _db = db;
        _extraction = extraction;
    }

    public async Task<(LayoutPlan? Plan, string? Error)> ImportImageAsync(byte[] content, string extension, string displayName, string uploadedBy, CancellationToken cancellationToken)
    {
        if (!ImageProbe.TryRead(content, extension, out var info, out var error))
        {
            return (null, error);
        }

        var storedExtension = info.ContentType == "image/png" ? ".png" : ".jpg";
        var sourceName = $"{Guid.NewGuid():N}{storedExtension}";
        var document = await _extraction.ExtractAsync(
            new LayoutExtractionRequest(content, info.ContentType, info.Width, info.Height, sourceName),
            cancellationToken);
        if (string.Equals(document.Extraction.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            var reason = string.Join(" ", document.Extraction.Warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
            return (null, string.IsNullOrWhiteSpace(reason) ? "ChatGPT extraction failed." : reason);
        }

        NoteValidation(document);
        return (CreatePlan(content, info, displayName, uploadedBy, sourceName, document), null);
    }

    public (LayoutPlan? Plan, string? Error) ImportManual(byte[] content, string extension, string displayName, string uploadedBy, IReadOnlyList<ManualPlotInput> plots)
    {
        if (!ImageProbe.TryRead(content, extension, out var info, out var error))
        {
            return (null, error);
        }

        if (plots.Count == 0)
        {
            return (null, "Add at least one plot.");
        }

        if (plots.Any(plot => string.IsNullOrWhiteSpace(plot.PlotNumber)))
        {
            return (null, "Every plot needs a plot number.");
        }

        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (plots.Any(plot => !numbers.Add(plot.PlotNumber.Trim())))
        {
            return (null, "Plot numbers must be unique.");
        }

        var storedExtension = info.ContentType == "image/png" ? ".png" : ".jpg";
        var sourceName = $"{Guid.NewGuid():N}{storedExtension}";
        var document = LayoutExtractionService.EmptyDraft(
            new LayoutExtractionRequest(content, info.ContentType, info.Width, info.Height, sourceName),
            "manual");
        document.CanvasWidth = info.Width;
        document.CanvasHeight = info.Height;
        document.Coordinates.DimensionUnit = "feet";
        document.Coordinates.AreaUnit = "cent";
        document.Extraction.Status = "needs-review";
        document.Extraction.Warnings.Add("Plot numbers were entered manually. Enter each area on the sketch page.");
        var index = 0;
        foreach (var item in plots)
        {
            index++;
            document.Plots.Add(new DocumentPlot
            {
                Id = $"plot-{index}",
                PlotNumber = item.PlotNumber.Trim(),
                Status = "unknown",
                Vertices =
                [
                    new() { X = 0.1, Y = 0.1 },
                    new() { X = 0.2, Y = 0.1 },
                    new() { X = 0.2, Y = 0.2 }
                ]
            });
        }

        NoteValidation(document);
        return (CreatePlan(content, info, displayName, uploadedBy, sourceName, document), null);
    }

    public static (LayoutPlan? Plan, string? Error) ImportJson(string json, string fileName, string uploadedBy)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return (null, $"The file is not valid JSON. {ex.Message}");
        }

        using (parsed)
        {
            var version = parsed.RootElement.TryGetProperty("schemaVersion", out var versionElement) ? versionElement.GetString() : null;
            if (string.Equals(version, SketchParser.SupportedVersion, StringComparison.Ordinal))
            {
                if (IsAppSketch(parsed.RootElement))
                {
                    return SketchParser.TryParse(json, fileName, uploadedBy, out var sketch, out var errors)
                        ? (sketch, null)
                        : (null, string.Join(" ", errors));
                }

                return ImportMeasuredLayout(parsed.RootElement, fileName, uploadedBy);
            }

            if (!string.Equals(version, LayoutDocumentValidator.SchemaVersion, StringComparison.Ordinal))
            {
                return (null, "schemaVersion must be \"1.0\" or \"2.0\".");
            }
        }

        LayoutDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<LayoutDocument>(json, LayoutExtractionService.JsonOptions);
        }
        catch (JsonException ex)
        {
            return (null, $"The file is not valid JSON. {ex.Message}");
        }

        if (document is null)
        {
            return (null, "The layout document is empty.");
        }

        var report = LayoutDocumentValidator.Validate(document);
        if (!report.IsValid)
        {
            return (null, string.Join(" ", report.Errors));
        }

        document.Extraction ??= new ExtractionInfo();
        document.Extraction.Warnings.AddRange(report.Warnings.Where(warning => !document.Extraction.Warnings.Contains(warning)));
        if (string.IsNullOrWhiteSpace(document.Extraction.Provider))
        {
            document.Extraction.Provider = "layout-document";
        }

        if (string.IsNullOrWhiteSpace(document.Extraction.Status))
        {
            document.Extraction.Status = "needs-review";
        }

        var plan = new LayoutPlan
        {
            Name = string.IsNullOrWhiteSpace(document.Name) ? "Untitled layout" : document.Name.Trim(),
            Unit = document.Coordinates.AreaUnit ?? "",
            Notes = Path.GetFileName(fileName),
            FileName = Path.GetFileName(fileName),
            UploadedBy = uploadedBy,
            UploadedOn = DateTime.UtcNow,
            DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions)
        };
        SyncPlots(plan, document);
        return (plan, null);
    }

    private static bool IsAppSketch(JsonElement root)
    {
        if (!root.TryGetProperty("unit", out var unit) || unit.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return !root.TryGetProperty("plots", out var plots)
            || plots.ValueKind != JsonValueKind.Array
            || plots.GetArrayLength() == 0
            || !plots[0].TryGetProperty("boundaryMeasurementsFeet", out _);
    }

    private static (LayoutPlan? Plan, string? Error) ImportMeasuredLayout(JsonElement root, string fileName, string uploadedBy)
    {
        var width = ReadNumber(root, "sourceImage", "widthPixels");
        var height = ReadNumber(root, "sourceImage", "heightPixels");
        if (width <= 0 || height <= 0)
        {
            return (null, "The layout JSON needs sourceImage.widthPixels and sourceImage.heightPixels.");
        }

        if (!root.TryGetProperty("plots", out var plots) || plots.ValueKind != JsonValueKind.Array || plots.GetArrayLength() == 0)
        {
            return (null, "The layout JSON needs at least one plot.");
        }

        var name = root.TryGetProperty("layoutName", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        var document = new LayoutDocument
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled layout" : name.Trim(),
            CanvasWidth = (int)Math.Ceiling(width),
            CanvasHeight = (int)Math.Ceiling(height),
            Coordinates = new CoordinateMetadata { DimensionUnit = "feet", AreaUnit = "cent" },
            Extraction = new ExtractionInfo
            {
                Provider = "layout-json",
                Status = "needs-review",
                Warnings = ["Plot numbers and side lengths were read from the layout JSON. Area is calculated in cents."]
            }
        };

        var index = 0;
        foreach (var plot in plots.EnumerateArray())
        {
            index++;
            var number = ReadPlotNumber(plot);
            if (string.IsNullOrWhiteSpace(number))
            {
                return (null, $"plots[{index - 1}] needs a plot number.");
            }

            var sides = ReadSides(plot);
            var vertices = ReadVertices(plot, width, height);
            if (vertices.Count < 3)
            {
                return (null, $"Plot {number} needs at least 3 vertices.");
            }

            document.Plots.Add(new DocumentPlot
            {
                Id = $"plot-{index}",
                PlotNumber = number,
                Sides = sides,
                Vertices = vertices,
                Facing = plot.TryGetProperty("facing", out var facing) && facing.ValueKind == JsonValueKind.String ? facing.GetString() : null,
                Status = "unknown"
            });
        }

        if (root.TryGetProperty("roads", out var roads) && roads.ValueKind == JsonValueKind.Array)
        {
            var roadIndex = 0;
            foreach (var road in roads.EnumerateArray())
            {
                roadIndex++;
                var vertices = ReadVertices(road, width, height);
                if (vertices.Count < 3)
                {
                    continue;
                }

                var label = road.TryGetProperty("label", out var labelElement) && labelElement.ValueKind == JsonValueKind.String
                    ? labelElement.GetString()
                    : $"Road {roadIndex}";
                document.Roads.Add(new DocumentRoad
                {
                    Id = $"road-{roadIndex}",
                    Name = label,
                    Width = ReadDecimal(road, "widthFeet"),
                    WidthUnit = "feet",
                    Vertices = vertices
                });
            }
        }

        PrintedLayoutSketch.FillAreaFromSides(document);
        return SaveDocumentPlan(document, fileName, uploadedBy);
    }

    private static (LayoutPlan? Plan, string? Error) SaveDocumentPlan(LayoutDocument document, string fileName, string uploadedBy)
    {
        var report = LayoutDocumentValidator.Validate(document);
        if (!report.IsValid)
        {
            return (null, string.Join(" ", report.Errors));
        }

        document.Extraction ??= new ExtractionInfo();
        document.Extraction.Warnings.AddRange(report.Warnings.Where(warning => !document.Extraction.Warnings.Contains(warning)));
        var plan = new LayoutPlan
        {
            Name = string.IsNullOrWhiteSpace(document.Name) ? "Untitled layout" : document.Name.Trim(),
            Unit = document.Coordinates.AreaUnit ?? "",
            Notes = Path.GetFileName(fileName),
            FileName = Path.GetFileName(fileName),
            UploadedBy = uploadedBy,
            UploadedOn = DateTime.UtcNow,
            DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions)
        };
        SyncPlots(plan, document);
        return (plan, null);
    }

    private static string? ReadPlotNumber(JsonElement plot)
    {
        if (!plot.TryGetProperty("plotNumber", out var number))
        {
            return null;
        }

        return number.ValueKind switch
        {
            JsonValueKind.String => number.GetString()?.Trim(),
            JsonValueKind.Number => number.TryGetInt64(out var whole) ? whole.ToString() : number.GetRawText(),
            _ => null
        };
    }

    private static PlotSides ReadSides(JsonElement plot)
    {
        if (!plot.TryGetProperty("boundaryMeasurementsFeet", out var sides) || sides.ValueKind != JsonValueKind.Object)
        {
            return new PlotSides();
        }

        return new PlotSides
        {
            Top = ReadDecimal(sides, "top"),
            Right = ReadDecimal(sides, "right"),
            Bottom = ReadDecimal(sides, "bottom"),
            Left = ReadDecimal(sides, "left")
        };
    }

    private static List<NormPoint> ReadVertices(JsonElement owner, double width, double height)
    {
        if (!owner.TryGetProperty("vertices", out var vertices) || vertices.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var points = new List<NormPoint>();
        foreach (var point in vertices.EnumerateArray())
        {
            if (!point.TryGetProperty("x", out var xElement) || !point.TryGetProperty("y", out var yElement))
            {
                continue;
            }

            if (!xElement.TryGetDouble(out var x) || !yElement.TryGetDouble(out var y))
            {
                continue;
            }

            if (x > 1 || y > 1)
            {
                x /= width;
                y /= height;
            }

            points.Add(new NormPoint { X = Math.Clamp(x, 0, 1), Y = Math.Clamp(y, 0, 1) });
        }

        return points;
    }

    private static double ReadNumber(JsonElement root, string objectName, string propertyName)
    {
        if (!root.TryGetProperty(objectName, out var owner) || owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.TryGetDouble(out var number) ? number : 0;
    }

    private static decimal? ReadDecimal(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static void NoteValidation(LayoutDocument document)
    {
        var report = LayoutDocumentValidator.Validate(document);
        document.Extraction.Warnings.AddRange(report.Warnings);
        document.Extraction.Warnings.AddRange(report.Errors.Select(item => $"Validation: {item}"));
        if (report.Errors.Count > 0)
        {
            document.Extraction.Status = "needs-review";
        }
    }

    private static LayoutPlan CreatePlan(byte[] content, ImageInfo info, string displayName, string uploadedBy, string sourceName, LayoutDocument document)
    {
        var plan = new LayoutPlan
        {
            Name = string.IsNullOrWhiteSpace(document.Name) ? "Untitled layout" : document.Name.Trim(),
            Unit = document.Coordinates.AreaUnit ?? "",
            Notes = Path.GetFileName(displayName),
            FileName = sourceName,
            UploadedBy = uploadedBy,
            UploadedOn = DateTime.UtcNow,
            DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions),
            Image = new LayoutImage { Content = content, ContentType = info.ContentType }
        };
        SyncPlots(plan, document);
        return plan;
    }

    public static string? SaveDocument(LayoutPlan plan, LayoutDocument document, out LayoutValidationReport report)
    {
        document.SchemaVersion = LayoutDocumentValidator.SchemaVersion;
        document.LayoutId = plan.Id.ToString();
        report = LayoutDocumentValidator.Validate(document);
        if (!report.IsValid)
        {
            return string.Join(" ", report.Errors);
        }

        document.Extraction.Status = "needs-review";
        document.Extraction.Warnings = document.Extraction.Warnings
            .Where(warning => !warning.StartsWith("Validation:", StringComparison.Ordinal))
            .Concat(report.Warnings)
            .Distinct()
            .ToList();
        plan.Name = string.IsNullOrWhiteSpace(document.Name) ? plan.Name : document.Name.Trim();
        plan.Unit = document.Coordinates.AreaUnit ?? plan.Unit;
        plan.DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions);
        SyncPlots(plan, document);
        return null;
    }

    public static void ApplyExtractedAreas(LayoutPlan plan, LayoutDocument extracted)
    {
        var document = ReadDocument(plan);
        document.Name = string.IsNullOrWhiteSpace(plan.Name) ? document.Name : plan.Name;
        document.Coordinates.DimensionUnit = "feet";
        document.Coordinates.AreaUnit = "cent";
        document.Extraction = extracted.Extraction ?? new ExtractionInfo();
        const string areaNote = "ChatGPT read the printed side lengths in feet. Area is stored in cents.";
        if (!document.Extraction.Warnings.Contains(areaNote))
        {
            document.Extraction.Warnings.Add(areaNote);
        }
        foreach (var source in extracted.Plots.Where(plot => !string.IsNullOrWhiteSpace(plot.PlotNumber)))
        {
            var number = source.PlotNumber!.Trim();
            var cents = source.Area?.Value ?? 0;
            var target = document.Plots.FirstOrDefault(plot => string.Equals(plot.PlotNumber, number, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                target = new DocumentPlot
                {
                    Id = string.IsNullOrWhiteSpace(source.Id) ? $"plot-{number}" : source.Id,
                    PlotNumber = number,
                    Status = string.IsNullOrWhiteSpace(source.Status) ? "unknown" : source.Status,
                    Vertices =
                    [
                        new() { X = 0, Y = 0 },
                        new() { X = 0.001, Y = 0 },
                        new() { X = 0, Y = 0.001 }
                    ]
                };
                document.Plots.Add(target);
            }

            target.Area = new MeasuredValue { Value = cents, Unit = "cent", Source = "extracted" };
            target.Length = source.Length;
            target.Breadth = source.Breadth;
            target.Sides = source.Sides;
            var plot = plan.Plots.FirstOrDefault(item => item.PlotNumber.Equals(number, StringComparison.OrdinalIgnoreCase));
            if (plot is null)
            {
                plot = new Plot { PlotNumber = number, Status = PlotStatus.Available, VerticesJson = "[]" };
                plan.Plots.Add(plot);
            }

            plot.Area = cents;
        }

        plan.Unit = "cent";
        plan.DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions);
    }

    public static LayoutDocument ReadDocument(LayoutPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.DocumentJson))
        {
            return JsonSerializer.Deserialize<LayoutDocument>(plan.DocumentJson, LayoutExtractionService.JsonOptions) ?? new LayoutDocument();
        }

        return FromExistingPlots(plan);
    }

    public static void SyncPlots(LayoutPlan plan, LayoutDocument document)
    {
        var incoming = document.Plots.Where(plot => !string.IsNullOrWhiteSpace(plot.PlotNumber)).ToList();
        var numbers = incoming.Select(plot => plot.PlotNumber!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        plan.Plots.RemoveAll(plot =>
            !numbers.Contains(plot.PlotNumber)
            && plot.AdvanceReceived == 0
            && string.IsNullOrWhiteSpace(plot.ClientName)
            && string.IsNullOrWhiteSpace(plot.Comment)
            && plot.Survey is null
            && plot.AgentId is null
            && plot.ClientId is null);
        foreach (var source in incoming)
        {
            var number = source.PlotNumber!.Trim();
            var plot = plan.Plots.FirstOrDefault(item => item.PlotNumber.Equals(number, StringComparison.OrdinalIgnoreCase));
            if (plot is null)
            {
                plot = new Plot { PlotNumber = number };
                plan.Plots.Add(plot);
            }

            plot.Area = source.Area?.Value ?? 0;
            plot.LengthFeet = source.Length?.Value ?? 0;
            plot.BreadthFeet = source.Breadth?.Value ?? 0;
            plot.TopFeet = source.Sides?.Top ?? 0;
            plot.RightFeet = source.Sides?.Right ?? 0;
            plot.BottomFeet = source.Sides?.Bottom ?? 0;
            plot.LeftFeet = source.Sides?.Left ?? 0;
            plot.Facing = source.Facing;
            plot.VerticesJson = JsonSerializer.Serialize(source.Vertices, LayoutExtractionService.JsonOptions);
            plot.Status = source.Status switch
            {
                "blocked" or "reserved" => PlotStatus.Blocked,
                "sold" => PlotStatus.Sold,
                "available" => PlotStatus.Available,
                _ => plot.Id == 0 ? PlotStatus.Available : plot.Status
            };
        }
    }

    private static LayoutDocument FromExistingPlots(LayoutPlan plan)
    {
        var points = plan.Plots.SelectMany(plot => SketchParser.ReadVertices(plot.VerticesJson)).ToList();
        var width = points.Count == 0 ? 1d : Math.Max(1d, points.Max(point => (double)point.X));
        var height = points.Count == 0 ? 1d : Math.Max(1d, points.Max(point => (double)point.Y));
        return new LayoutDocument
        {
            Name = plan.Name,
            CanvasWidth = (int)Math.Ceiling(width),
            CanvasHeight = (int)Math.Ceiling(height),
            Coordinates = new CoordinateMetadata { DimensionUnit = "feet", AreaUnit = string.IsNullOrWhiteSpace(plan.Unit) ? null : plan.Unit },
            Plots = plan.Plots.Select(plot => new DocumentPlot
            {
                Id = $"plot-{plot.Id}",
                PlotNumber = plot.PlotNumber,
                Vertices = SketchParser.ReadVertices(plot.VerticesJson).Select(point => new NormPoint
                {
                    X = (double)point.X / width,
                    Y = 1 - ((double)point.Y / height)
                }).ToList(),
                Area = new MeasuredValue { Value = plot.Area, Unit = plan.Unit, Source = "manual" },
                Length = plot.LengthFeet > 0 ? new MeasuredValue { Value = plot.LengthFeet, Unit = "feet", Source = "manual" } : null,
                Breadth = plot.BreadthFeet > 0 ? new MeasuredValue { Value = plot.BreadthFeet, Unit = "feet", Source = "manual" } : null,
                Sides = plot.TopFeet > 0 || plot.RightFeet > 0 || plot.BottomFeet > 0 || plot.LeftFeet > 0
                    ? new PlotSides
                    {
                        Top = plot.TopFeet,
                        Right = plot.RightFeet,
                        Bottom = plot.BottomFeet,
                        Left = plot.LeftFeet
                    }
                    : null,
                Facing = plot.Facing,
                Status = plot.Status switch
                {
                    PlotStatus.Blocked => "blocked",
                    PlotStatus.Sold => "sold",
                    _ => "available"
                },
                Confidence = null
            }).ToList(),
            Extraction = new ExtractionInfo
            {
                Provider = "existing-record",
                Status = "needs-review",
                Warnings = ["This layout was normalized from stored plot outlines. It was not extracted from the image."]
            }
        };
    }
}
