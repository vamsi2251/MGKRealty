namespace LayoutManagementAPI.Models;

public enum LayoutAccessLevel
{
    ReadOnly = 0,
    Editable = 1
}

public class LayoutAccess
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public LayoutPlan? LayoutPlan { get; set; }
    public string UserId { get; set; } = "";
    public LayoutAccessLevel Level { get; set; }
}

public class LayoutPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "sqyd";
    public string? Notes { get; set; }
    public string FileName { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public decimal OwnerPayable { get; set; }
    public decimal SurveyorAmount { get; set; }
    public int TipperCount { get; set; }
    public decimal TipperCost { get; set; }
    public decimal MiscellaneousAmount { get; set; }
    public string? DocumentJson { get; set; }
    public List<Plot> Plots { get; set; } = new();
    public List<OwnerPayment> OwnerPayments { get; set; } = new();
    public List<LayoutAccess> Accesses { get; set; } = new();
    public LayoutImage? Image { get; set; }
}

public class OwnerPayment
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public LayoutPlan? LayoutPlan { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
}

public class RegistrationReminder
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public int PlotId { get; set; }
    public string UserName { get; set; } = "";
    public DateTime ReminderDate { get; set; }
    public string SketchName { get; set; } = "";
    public string PlotNumber { get; set; } = "";
    public decimal AdvanceReceived { get; set; }
    public DateTime? AdvanceReceivedOn { get; set; }
    public DateTime RegistrationOn { get; set; }
    public DateTime? ReadOn { get; set; }
}

public class LayoutImage
{
    public int Id { get; set; }
    public int LayoutPlanId { get; set; }
    public LayoutPlan? LayoutPlan { get; set; }
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "image/jpeg";
}
