namespace LayoutManagementAPI.Contracts;

public class LayoutDocument
{
    public string SchemaVersion { get; set; } = "2.0";
    public string? LayoutId { get; set; }
    public string Name { get; set; } = "Untitled layout";
    public string? SourceImage { get; set; }
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public CoordinateMetadata Coordinates { get; set; } = new();
    public string? Orientation { get; set; }
    public LayoutBoundary? Boundary { get; set; }
    public List<DocumentPlot> Plots { get; set; } = new();
    public List<DocumentRoad> Roads { get; set; } = new();
    public List<DocumentAnnotation> Annotations { get; set; } = new();
    public ExtractionInfo Extraction { get; set; } = new();
}

public class CoordinateMetadata
{
    public string System { get; set; } = "normalized-image";
    public string Origin { get; set; } = "top-left";
    public string XAxis { get; set; } = "right";
    public string YAxis { get; set; } = "down";
    public string Unit { get; set; } = "fraction";
    public string? DimensionUnit { get; set; }
    public string? AreaUnit { get; set; }
}

public class LayoutBoundary
{
    public List<NormPoint> Vertices { get; set; } = new();
    public double? Confidence { get; set; }
}

public class DocumentPlot
{
    public string Id { get; set; } = "";
    public string? PlotNumber { get; set; }
    public List<NormPoint> Vertices { get; set; } = new();
    public MeasuredValue? Area { get; set; }
    public MeasuredValue? Length { get; set; }
    public MeasuredValue? Breadth { get; set; }
    public PlotSides? Sides { get; set; }
    public string? Facing { get; set; }
    public string Status { get; set; } = "unknown";
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class DocumentRoad
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public decimal? Width { get; set; }
    public string? WidthUnit { get; set; }
    public List<NormPoint> Vertices { get; set; } = new();
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class DocumentAnnotation
{
    public string Id { get; set; } = "";
    public string? Text { get; set; }
    public NormPoint? Position { get; set; }
    public double? Confidence { get; set; }
}

public class PlotSides
{
    public decimal? Top { get; set; }
    public decimal? Right { get; set; }
    public decimal? Bottom { get; set; }
    public decimal? Left { get; set; }
}

public class MeasuredValue
{
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string Source { get; set; } = "unverified";
}

public class NormPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class ExtractionInfo
{
    public string Provider { get; set; } = "none";
    public string Status { get; set; } = "needs-review";
    public double? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class LayoutValidationReport
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}
