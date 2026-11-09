using System.ComponentModel.DataAnnotations;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Contracts;

public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string LicenseNumber { get; set; } = "";
}

public class AgentRequest : IValidatableObject
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string? Email { get; set; }

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    [Required, MaxLength(40)]
    public string LicenseNumber { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Enter a valid email address.", [nameof(Email)]);
        }
    }
}

public class ClientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public ClientType ClientType { get; set; }
}

public class ClientRequest : IValidatableObject
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string? Email { get; set; }

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    public ClientType ClientType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Enter a valid email address.", [nameof(Email)]);
        }
    }
}

public class PropertyDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string StreetAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public PropertyType PropertyType { get; set; }
    public ListingType ListingType { get; set; }
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public int SquareFeet { get; set; }
    public ListingStatus Status { get; set; }
    public string Description { get; set; } = "";
    public DateTime ListedOn { get; set; }
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
}

public class PropertyRequest
{
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

    [Range(1, int.MaxValue)]
    public int AgentId { get; set; }
}

public class InquiryDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = "";
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Message { get; set; } = "";
    public InquiryStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class InquiryRequest
{
    [Range(1, int.MaxValue)]
    public int PropertyId { get; set; }

    [Range(1, int.MaxValue)]
    public int ClientId { get; set; }

    [Required, MaxLength(1000)]
    public string Message { get; set; } = "";

    public InquiryStatus Status { get; set; }
}

public class DashboardDto
{
    public int PropertyCount { get; set; }
    public int AvailableCount { get; set; }
    public int AgentCount { get; set; }
    public int ClientCount { get; set; }
    public int OpenInquiryCount { get; set; }
    public decimal ListedSaleValue { get; set; }
    public List<PropertyDto> RecentProperties { get; set; } = new();
}
