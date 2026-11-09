using System.ComponentModel.DataAnnotations;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Contracts;

public class SketchDocument
{
    public string SchemaVersion { get; set; } = "";
    public string LayoutName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string? Notes { get; set; }
    public List<SketchPlot> Plots { get; set; } = new();
}

public class SketchPlot
{
    public string PlotNumber { get; set; } = "";
    public decimal Area { get; set; }
    public decimal? LengthFeet { get; set; }
    public decimal? BreadthFeet { get; set; }
    public string? Facing { get; set; }
    public decimal? Price { get; set; }
    public List<SketchPoint> Vertices { get; set; } = new();
}

public class SketchPoint
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
}

public class ManualPlotInput
{
    public string PlotNumber { get; set; } = "";
    public decimal Top { get; set; }
    public decimal Right { get; set; }
    public decimal Bottom { get; set; }
    public decimal Left { get; set; }
}

public class LayoutSummaryDto
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

public class LayoutDetailDto
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
    public List<OwnerPaymentDto> OwnerPayments { get; set; } = new();
    public LayoutDocument? Document { get; set; }
    public List<PlotDto> Plots { get; set; } = new();
}

public class OwnerPaymentDto
{
    public int Id { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
}

public class OwnerPayableRequest
{
    [Range(typeof(decimal), "0", "100000000000")]
    public decimal OwnerPayable { get; set; }
}

public class OwnerPaymentRequest
{
    public DateTime PaidOn { get; set; }

    [Range(typeof(decimal), "0.01", "100000000000")]
    public decimal Amount { get; set; }
}

public class DevelopmentExpenseRequest
{
    [Range(typeof(decimal), "0", "100000000000")]
    public decimal SurveyorAmount { get; set; }

    [Range(0, 100000)]
    public int TipperCount { get; set; }

    [Range(typeof(decimal), "0", "100000000000")]
    public decimal TipperCost { get; set; }

    [Range(typeof(decimal), "0", "100000000000")]
    public decimal MiscellaneousAmount { get; set; }
}

public class RegistrationReminderDto
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

public class PlotDto
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
    public List<SketchPoint> Vertices { get; set; } = new();
}

public class PlotBookingRequest
{
    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Area { get; set; }

    [Range(typeof(decimal), "0", "100000000000")]
    public decimal Price { get; set; }

    public int AgentId { get; set; }

    public int ClientId { get; set; }

    [MaxLength(120)]
    public string? ClientName { get; set; }

    [MaxLength(20)]
    public string? ClientMobile { get; set; }

    [Range(typeof(decimal), "0", "100000000000")]
    public decimal AdvanceReceived { get; set; }

    public DateTime? AdvanceReceivedOn { get; set; }

    public DateTime? RegistrationOn { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public PlotStatus Status { get; set; }
}

public class RegisterRequest
{
    [Required, MaxLength(80)]
    public string FullName { get; set; } = "";

    [Required, MaxLength(60)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class AuthResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresOn { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public bool LoginRestricted { get; set; }
    public bool IsBuiltInAdmin { get; set; }
}

public class AccountCredentialsRequest
{
    [Required, MaxLength(60)]
    public string Username { get; set; } = "";

    [MaxLength(100)]
    public string? NewPassword { get; set; }
}

public class LoginAccessRequest
{
    public bool Allowed { get; set; }
}

public class RoleUpdateRequest
{
    [Required]
    public string Role { get; set; } = "";
}

public class LayoutPartnerDto
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public LayoutAccessLevel Level { get; set; }
}

public class LayoutPartnerRequest
{
    [Required]
    public string UserId { get; set; } = "";

    public LayoutAccessLevel Level { get; set; }
}

public class LayoutCatalogItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class UserSketchGrant
{
    public int LayoutId { get; set; }
    public LayoutAccessLevel Level { get; set; }
}

public class UserSketchAccessRequest
{
    public List<UserSketchGrant> Sketches { get; set; } = new();
}

public class CreateUserRequest
{
    [Required, MaxLength(80)]
    public string FullName { get; set; } = "";

    [Required, MaxLength(60)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = "";

    public List<UserSketchGrant> Sketches { get; set; } = new();
}
