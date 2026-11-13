using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Models;

public class Inquiry
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string ClientName { get; set; } = "";

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    public int? LayoutPlanId { get; set; }

    public LayoutPlan? LayoutPlan { get; set; }

    [MaxLength(1000)]
    public string Message { get; set; } = "";

    public DateTime CreatedOn { get; set; }
}
