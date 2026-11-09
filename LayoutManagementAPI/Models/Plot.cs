namespace LayoutManagementAPI.Models;

public class Plot
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public LayoutPlan? LayoutPlan { get; set; }
    public string PlotNumber { get; set; } = "";
    public decimal Area { get; set; }
    public decimal LengthFeet { get; set; }
    public decimal BreadthFeet { get; set; }
    public decimal TopFeet { get; set; }
    public decimal RightFeet { get; set; }
    public decimal BottomFeet { get; set; }
    public decimal LeftFeet { get; set; }
    public string? Facing { get; set; }
    public string VerticesJson { get; set; } = "[]";
    public decimal Price { get; set; }
    public PlotStatus Status { get; set; }
    public int? AgentId { get; set; }
    public int? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientMobile { get; set; }
    public decimal AdvanceReceived { get; set; }
    public DateTime? AdvanceReceivedOn { get; set; }
    public DateTime? RegistrationOn { get; set; }
    public DateTime? BlockedOn { get; set; }
    public string? Comment { get; set; }
    public PlotSurvey? Survey { get; set; }
}

public class PlotSurvey
{
    public int PlotId { get; set; }
    public Plot? Plot { get; set; }
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "image/jpeg";
    public string FileName { get; set; } = "";
}
