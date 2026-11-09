using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Models;

public class PropertyListing
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = "";

    [Required, MaxLength(200)]
    public string StreetAddress { get; set; } = "";

    [Required, MaxLength(80)]
    public string City { get; set; } = "";

    [Required, MaxLength(40)]
    public string State { get; set; } = "";

    [Required, MaxLength(20)]
    public string PostalCode { get; set; } = "";

    public PropertyType PropertyType { get; set; }

    public ListingType ListingType { get; set; }

    [Range(0, 1000000000)]
    public decimal Price { get; set; }

    [Range(0, 50)]
    public int Bedrooms { get; set; }

    [Range(0, 50)]
    public decimal Bathrooms { get; set; }

    [Range(0, 1000000)]
    public int SquareFeet { get; set; }

    public ListingStatus Status { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = "";

    public DateTime ListedOn { get; set; }

    public int AgentId { get; set; }

    public Agent? Agent { get; set; }

    public List<Inquiry> Inquiries { get; set; } = new();
}
