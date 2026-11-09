namespace LayoutManagementWebApp.Models;

public class LayoutDocumentModel
{
    public string SchemaVersion { get; set; } = "2.0";
    public string? LayoutId { get; set; }
    public string Name { get; set; } = "";
    public string? SourceImage { get; set; }
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public CoordinateMetadataModel Coordinates { get; set; } = new();
    public string? Orientation { get; set; }
    public List<DocumentPlotModel> Plots { get; set; } = new();
    public List<DocumentRoadModel> Roads { get; set; } = new();
    public List<DocumentAnnotationModel> Annotations { get; set; } = new();
    public ExtractionInfoModel Extraction { get; set; } = new();
}

public class CoordinateMetadataModel
{
    public string System { get; set; } = "normalized-image";
    public string Origin { get; set; } = "top-left";
    public string Unit { get; set; } = "fraction";
    public string? DimensionUnit { get; set; }
    public string? AreaUnit { get; set; }
}

public class DocumentPlotModel
{
    public string Id { get; set; } = "";
    public string? PlotNumber { get; set; }
    public List<NormPointModel> Vertices { get; set; } = new();
    public MeasuredValueModel? Area { get; set; }
    public MeasuredValueModel? Length { get; set; }
    public MeasuredValueModel? Breadth { get; set; }
    public PlotSidesModel? Sides { get; set; }
    public string? Facing { get; set; }
    public string Status { get; set; } = "unknown";
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class DocumentRoadModel
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public decimal? Width { get; set; }
    public string? WidthUnit { get; set; }
    public List<NormPointModel> Vertices { get; set; } = new();
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class DocumentAnnotationModel
{
    public string Id { get; set; } = "";
    public string? Text { get; set; }
    public NormPointModel? Position { get; set; }
}

public class PlotSidesModel
{
    public decimal? Top { get; set; }
    public decimal? Right { get; set; }
    public decimal? Bottom { get; set; }
    public decimal? Left { get; set; }
}

public class MeasuredValueModel
{
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string Source { get; set; } = "unverified";
}

public class NormPointModel
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class ExtractionInfoModel
{
    public string Provider { get; set; } = "none";
    public string Status { get; set; } = "needs-review";
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}
