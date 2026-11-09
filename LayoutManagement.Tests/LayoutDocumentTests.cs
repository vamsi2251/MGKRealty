using System.Drawing;
using System.Text.Json;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using Xunit;

namespace LayoutManagement.Tests;

public class LayoutDocumentTests
{
    [Fact]
    public void Printed_sheet_has_fifty_four_plots_and_plot_two_area_from_sides()
    {
        var document = PrintedLayoutSketch.Build(new LayoutExtractionRequest([], "image/jpeg", 1000, 1400, "sheet.jpg"));
        var plot2 = document.Plots.Single(plot => plot.PlotNumber == "2");
        var expected = 30m * 72.6m / PrintedLayoutSketch.SqFtPerCent;

        Assert.Equal(54, document.Plots.Count);
        Assert.Equal(72.6m, plot2.Length!.Value);
        Assert.Equal(30m, plot2.Breadth!.Value);
        Assert.Equal(expected, plot2.Area!.Value);
        Assert.Equal("cent", plot2.Area.Unit);
        Assert.Equal("feet", document.Coordinates.DimensionUnit);
    }

    [Fact]
    public void Document_round_trips_through_json()
    {
        var document = LayoutExtractionService.EmptyDraft(new LayoutExtractionRequest([1], "image/png", 800, 600, "file.png"), "none");
        document.Plots.Add(new DocumentPlot
        {
            Id = "p1",
            PlotNumber = "1",
            Status = "unknown",
            Vertices = [new NormPoint { X = 0.1, Y = 0.1 }, new NormPoint { X = 0.2, Y = 0.1 }, new NormPoint { X = 0.2, Y = 0.2 }]
        });

        var json = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions);
        var copy = JsonSerializer.Deserialize<LayoutDocument>(json, LayoutExtractionService.JsonOptions);

        Assert.NotNull(copy);
        Assert.Equal("2.0", copy.SchemaVersion);
        Assert.Equal("p1", copy.Plots[0].Id);
        Assert.Equal(0.2, copy.Plots[0].Vertices[1].X);
    }

    [Fact]
    public void Normalized_point_maps_onto_the_canvas_without_changing_aspect()
    {
        var (x, y) = LayoutDocumentValidator.ToCanvas(new NormPoint { X = 0.25, Y = 0.5 }, 800, 400);
        Assert.Equal(200, x);
        Assert.Equal(200, y);
    }

    [Fact]
    public void Validator_rejects_duplicate_ids_and_short_polygons()
    {
        var document = ValidDocument();
        document.Plots.Add(new DocumentPlot { Id = "same", PlotNumber = "2", Vertices = [new NormPoint { X = 0, Y = 0 }, new NormPoint { X = 1, Y = 0 }] });
        document.Plots.Add(new DocumentPlot { Id = "same", PlotNumber = "3", Vertices = [new NormPoint { X = 0, Y = 0 }, new NormPoint { X = 0.2, Y = 0 }, new NormPoint { X = 0.2, Y = 0.2 }] });

        var report = LayoutDocumentValidator.Validate(document);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, error => error.Contains("unique id"));
        Assert.Contains(report.Errors, error => error.Contains("at least 3"));
    }

    [Fact]
    public void Validator_warns_when_area_has_no_survey_scale()
    {
        var document = ValidDocument();
        document.Plots[0].Area = new MeasuredValue { Value = 5.51m, Unit = "cent", Source = "extracted" };

        var report = LayoutDocumentValidator.Validate(document);

        Assert.True(report.IsValid);
        Assert.Contains(report.Warnings, warning => warning.Contains("no surveyed scale"));
    }

    [Fact]
    public void Image_probe_rejects_a_file_that_is_not_a_png_or_jpeg()
    {
        var ok = ImageProbe.TryRead([0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0], ".png", out _, out var error);
        Assert.False(ok);
        Assert.Contains("readable", error);
    }

    [Fact]
    public void Scent_layout_image_reads_plot_areas_and_roads()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "scent-layout.png");
        var document = LineSketchExtractor.Extract(new LayoutExtractionRequest(File.ReadAllBytes(path), "image/png", 0, 0, "layout.png"));

        Assert.Equal(new decimal?[] { 15, 4.8m, 15, 10, 12, 30, 30, 30 }, document.Plots.Select(plot => plot.Area?.Value).ToArray());
        Assert.Equal(3, document.Roads.Count);
        Assert.Equal(2, document.Roads.Count(road => road.Width == 20));
        Assert.DoesNotContain(document.Roads, road => road.Width is not null && road.Width != 20);
        Assert.Equal("needs-review", document.Extraction.Status);
    }

    [Fact]
    public void Easy_kothapeta_sketch_reads_every_plot_and_road()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "kothapeta-easy-sketch.png");
        var document = LineSketchExtractor.Extract(new LayoutExtractionRequest(File.ReadAllBytes(path), "image/png", 0, 0, "kothapeta-easy-sketch.png"));

        Assert.Equal(new decimal?[] { 15, 4.8m, 15, 10, 12, 30, 30, 30 }, document.Plots.Select(plot => plot.Area?.Value).ToArray());
        Assert.Equal(3, document.Roads.Count);
        Assert.All(document.Roads, road => Assert.Equal(20m, road.Width));
        Assert.Equal("needs-review", document.Extraction.Status);
    }

    [Fact]
    public void Line_sketch_traces_closed_rectangles_without_inventing_areas()
    {
        using var image = new Bitmap(360, 220);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
        }
        DrawRect(image, 20, 20, 320, 180);
        DrawVertical(image, 180, 20, 180);
        using var buffer = new MemoryStream();
        image.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
        var document = LineSketchExtractor.Extract(new LayoutExtractionRequest(buffer.ToArray(), "image/png", 360, 220, "grid.png"), readLabels: false);

        Assert.Equal(2, document.Plots.Count);
        Assert.All(document.Plots, plot => Assert.Null(plot.Area));
        Assert.All(document.Plots, plot => Assert.Equal(4, plot.Vertices.Count));
        Assert.Contains(document.Extraction.Warnings, warning => warning.Contains("not survey coordinates"));
    }

    private static void DrawVertical(Bitmap image, int x, int y, int height)
    {
        var black = Color.Black;
        for (var offset = 0; offset < 3; offset++)
        {
            for (var py = y; py < y + height; py++)
            {
                image.SetPixel(x + offset, py, black);
            }
        }
    }

    private static void DrawRect(Bitmap image, int x, int y, int width, int height)
    {
        var black = Color.Black;
        for (var offset = 0; offset < 3; offset++)
        {
            for (var px = x; px < x + width; px++)
            {
                image.SetPixel(px, y + offset, black);
                image.SetPixel(px, y + height - 1 - offset, black);
            }

            for (var py = y; py < y + height; py++)
            {
                image.SetPixel(x + offset, py, black);
                image.SetPixel(x + width - 1 - offset, py, black);
            }
        }
    }

    [Fact]
    public void Four_sides_of_30_by_72_6_feet_are_5_cents()
    {
        var measured = PrintedLayoutSketch.TryMeasure(
            new PlotSides { Top = 30m, Bottom = 30m, Left = 72.6m, Right = 72.6m },
            out var length,
            out var breadth,
            out var area);

        Assert.True(measured);
        Assert.Equal(72.6m, length);
        Assert.Equal(30m, breadth);
        Assert.Equal(5m, area);
        Assert.Equal(5m, PrintedLayoutSketch.AreaInCents(72.6m, 30m));
    }

    [Fact]
    public void Unconfigured_extraction_does_not_invent_plots()
    {
        var draft = LayoutExtractionService.EmptyDraft(new LayoutExtractionRequest([], "image/png", 640, 480, "scan.png"), "none");
        draft.Extraction.Warnings.Add("No vision provider is configured.");

        Assert.Empty(draft.Plots);
        Assert.Equal("needs-review", draft.Extraction.Status);
        Assert.Null(draft.Extraction.Confidence);
    }

    [Fact]
    public void Measured_layout_json_accepts_numeric_plot_numbers_and_side_strings()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "layoutName": "Plot Layout - 1 Plot",
              "sourceImage": { "widthPixels": 1000, "heightPixels": 1000 },
              "plots": [
                {
                  "plotNumber": 2,
                  "boundaryMeasurementsFeet": { "top": "30", "bottom": "30", "left": "72.6", "right": "72.6" },
                  "vertices": [ { "x": 100, "y": 100 }, { "x": 400, "y": 100 }, { "x": 400, "y": 500 } ]
                }
              ]
            }
            """;

        var imported = LayoutWorkflow.ImportJson(json, "plot-layout.json", "admin");

        Assert.Null(imported.Error);
        var plot = Assert.Single(imported.Plan!.Plots);
        Assert.Equal("2", plot.PlotNumber);
        Assert.Equal(5m, plot.Area);
        Assert.Equal("cent", imported.Plan.Unit);
    }

    [Fact]
    public void Downloaded_plot_layout_imports_all_numbered_plots()
    {
        var path = @"c:\Users\VamsiGuddanti\Downloads\plot-layout.json";
        if (!File.Exists(path))
        {
            return;
        }

        var imported = LayoutWorkflow.ImportJson(File.ReadAllText(path), "plot-layout.json", "admin");

        Assert.Null(imported.Error);
        Assert.Equal(35, imported.Plan!.Plots.Count);
        Assert.Equal("1", imported.Plan.Plots.Single(plot => plot.PlotNumber == "1").PlotNumber);
        Assert.True(imported.Plan.Plots.Single(plot => plot.PlotNumber == "1").Area > 0);
    }

    private static LayoutDocument ValidDocument() => new()
    {
        CanvasWidth = 100,
        CanvasHeight = 100,
        Plots =
        [
            new DocumentPlot
            {
                Id = "p1",
                PlotNumber = "1",
                Status = "unknown",
                Vertices =
                [
                    new NormPoint { X = 0.1, Y = 0.1 },
                    new NormPoint { X = 0.3, Y = 0.1 },
                    new NormPoint { X = 0.3, Y = 0.3 }
                ]
            }
        ]
    };
}
