namespace LayoutManagementAPI.Models;

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

public enum PlotStatus
{
    Available = 0,
    Blocked = 1,
    Sold = 2
}
