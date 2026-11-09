namespace LayoutManagementWebApp.Models;

public class SketchPointModel
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
}

public class ManualPlotInputModel
{
    public string PlotNumber { get; set; } = "";
    public decimal Top { get; set; }
    public decimal Right { get; set; }
    public decimal Bottom { get; set; }
    public decimal Left { get; set; }
}

public class LayoutSummaryModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string FileName { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
    public int AvailableCount { get; set; }
    public int BlockedCount { get; set; }
    public decimal AdvanceTotal { get; set; }
}

public class LayoutDetailModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string? Notes { get; set; }
    public string FileName { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
    public bool HasImage { get; set; }
    public bool CanEdit { get; set; }
    public bool CanManage { get; set; }
    public bool CanShare { get; set; }
    public decimal OwnerPayable { get; set; }
    public decimal OwnerPaid { get; set; }
    public decimal OwnerRemaining { get; set; }
    public decimal SurveyorAmount { get; set; }
    public int TipperCount { get; set; }
    public decimal TipperCost { get; set; }
    public decimal TipperTotal { get; set; }
    public decimal MiscellaneousAmount { get; set; }
    public decimal DevelopmentTotal { get; set; }
    public List<OwnerPaymentModel> OwnerPayments { get; set; } = new();
    public LayoutDocumentModel? Document { get; set; }
    public List<PlotModel> Plots { get; set; } = new();
}

public class OwnerPaymentModel
{
    public int Id { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
}

public class RegistrationReminderModel
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public string SketchName { get; set; } = "";
    public string PlotNumber { get; set; } = "";
    public decimal AdvanceReceived { get; set; }
    public DateTime? AdvanceReceivedOn { get; set; }
    public DateTime RegistrationOn { get; set; }
    public DateTime ReminderDate { get; set; }
    public bool IsRead { get; set; }
}

public class PlotModel
{
    public int Id { get; set; }
    public string PlotNumber { get; set; } = "";
    public decimal Area { get; set; }
    public decimal LengthFeet { get; set; }
    public decimal BreadthFeet { get; set; }
    public decimal TopFeet { get; set; }
    public decimal RightFeet { get; set; }
    public decimal BottomFeet { get; set; }
    public decimal LeftFeet { get; set; }
    public string Unit { get; set; } = "";
    public string? Facing { get; set; }
    public decimal Price { get; set; }
    public PlotStatus Status { get; set; }
    public int AgentId { get; set; }
    public string? AgentName { get; set; }
    public int ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientMobile { get; set; }
    public decimal AdvanceReceived { get; set; }
    public DateTime? AdvanceReceivedOn { get; set; }
    public DateTime? RegistrationOn { get; set; }
    public decimal Balance { get; set; }
    public DateTime? BlockedOn { get; set; }
    public string? Comment { get; set; }
    public bool HasSurvey { get; set; }
    public string? SurveyFileName { get; set; }
    public List<SketchPointModel> Vertices { get; set; } = new();
}

public enum PlotStatus
{
    Available = 0,
    Blocked = 1,
    Sold = 2
}
