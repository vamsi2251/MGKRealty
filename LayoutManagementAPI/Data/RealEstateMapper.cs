using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Data;

public static class RealEstateMapper
{
    public static AgentDto ToDto(this Agent agent) => new()
    {
        Id = agent.Id,
        Name = agent.Name,
        Email = agent.Email,
        Phone = agent.Phone,
        LicenseNumber = agent.LicenseNumber
    };

    public static ClientDto ToDto(this Client client) => new()
    {
        Id = client.Id,
        Name = client.Name,
        Email = client.Email,
        Phone = client.Phone,
        ClientType = client.ClientType
    };

    public static PropertyDto ToDto(this PropertyListing property) => new()
    {
        Id = property.Id,
        Title = property.Title,
        StreetAddress = property.StreetAddress,
        City = property.City,
        State = property.State,
        PostalCode = property.PostalCode,
        PropertyType = property.PropertyType,
        ListingType = property.ListingType,
        Price = property.Price,
        Bedrooms = property.Bedrooms,
        Bathrooms = property.Bathrooms,
        SquareFeet = property.SquareFeet,
        Status = property.Status,
        Description = property.Description,
        ListedOn = property.ListedOn,
        AgentId = property.AgentId,
        AgentName = property.Agent?.Name ?? ""
    };

    public static InquiryDto ToDto(this Inquiry inquiry) => new()
    {
        Id = inquiry.Id,
        ClientName = inquiry.ClientName,
        Phone = inquiry.Phone,
        LayoutPlanId = inquiry.LayoutPlanId,
        LayoutName = inquiry.LayoutPlan?.Name ?? "",
        Message = inquiry.Message,
        CreatedOn = inquiry.CreatedOn
    };

    public static void Apply(this Agent agent, AgentRequest request)
    {
        agent.Name = request.Name.Trim();
        agent.Email = request.Email?.Trim() ?? "";
        agent.Phone = request.Phone.Trim();
        agent.LicenseNumber = request.LicenseNumber.Trim();
    }

    public static void Apply(this Client client, ClientRequest request)
    {
        client.Name = request.Name.Trim();
        client.Email = request.Email?.Trim() ?? "";
        client.Phone = request.Phone.Trim();
        client.ClientType = request.ClientType;
    }

    public static void Apply(this PropertyListing property, PropertyRequest request)
    {
        property.Title = request.Title.Trim();
        property.StreetAddress = request.StreetAddress.Trim();
        property.City = request.City.Trim();
        property.State = request.State.Trim();
        property.PostalCode = request.PostalCode.Trim();
        property.PropertyType = request.PropertyType;
        property.ListingType = request.ListingType;
        property.Price = request.Price;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.SquareFeet = request.SquareFeet;
        property.Status = request.Status;
        property.Description = request.Description.Trim();
        property.AgentId = request.AgentId;
    }

    public static void Apply(this Inquiry inquiry, InquiryRequest request)
    {
        inquiry.ClientName = request.ClientName.Trim();
        inquiry.Phone = request.Phone.Trim();
        inquiry.LayoutPlanId = request.LayoutPlanId;
        inquiry.Message = (request.Message ?? "").Trim();
    }
}
