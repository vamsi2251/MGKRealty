namespace LayoutManagementWebApp.Models;

public enum PropertyType
{
    SingleFamily = 0,
    Condo = 1,
    Townhouse = 2,
    Apartment = 3,
    Land = 4
}

public enum ListingType
{
    Sale = 0,
    Rent = 1
}

public enum ListingStatus
{
    Available = 0,
    Pending = 1,
    Sold = 2,
    Rented = 3
}

public enum ClientType
{
    Buyer = 0,
    Seller = 1,
    Tenant = 2
}

public enum InquiryStatus
{
    New = 0,
    Contacted = 1,
    Closed = 2
}

public class AgentModel : System.ComponentModel.DataAnnotations.IValidatableObject
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string Name { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string Phone { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(40)]
    public string LicenseNumber { get; set; } = "";

    public System.Collections.Generic.IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Enter a valid email address.", [nameof(Email)]);
        }
    }
}

public class ClientModel : System.ComponentModel.DataAnnotations.IValidatableObject
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string Name { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string Phone { get; set; } = "";

    public ClientType ClientType { get; set; }

    public System.Collections.Generic.IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Enter a valid email address.", [nameof(Email)]);
        }
    }
}

public class PropertyModel
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(160)]
    public string Title { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string StreetAddress { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string City { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(40)]
    public string State { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string PostalCode { get; set; } = "";

    public PropertyType PropertyType { get; set; }
    public ListingType ListingType { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000000)]
    public decimal Price { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 50)]
    public int Bedrooms { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 50)]
    public decimal Bathrooms { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000)]
    public int SquareFeet { get; set; }

    public ListingStatus Status { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string Description { get; set; } = "";

    public DateTime ListedOn { get; set; }
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
}

public class InquiryModel
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string ClientName { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string Phone { get; set; } = "";

    public int? LayoutPlanId { get; set; }
    public string LayoutName { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string Message { get; set; } = "";

    public DateTime CreatedOn { get; set; }
}

public class DashboardModel
{
    public int PropertyCount { get; set; }
    public int AvailableCount { get; set; }
    public int AgentCount { get; set; }
    public int ClientCount { get; set; }
    public int OpenInquiryCount { get; set; }
    public decimal ListedSaleValue { get; set; }
    public List<PropertyModel> RecentProperties { get; set; } = new();
}
