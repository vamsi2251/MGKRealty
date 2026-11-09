using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Models;

public class Inquiry
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public PropertyListing? Property { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    [Required, MaxLength(1000)]
    public string Message { get; set; } = "";

    public InquiryStatus Status { get; set; }

    public DateTime CreatedOn { get; set; }
}
