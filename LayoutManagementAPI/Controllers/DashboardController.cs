using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize(Roles = Roles.Staff)]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public DashboardController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var properties = await _db.Properties.Include(p => p.Agent)
            .OrderByDescending(p => p.ListedOn)
            .ToListAsync();

        return new DashboardDto
        {
            PropertyCount = properties.Count,
            AvailableCount = properties.Count(p => p.Status == ListingStatus.Available),
            AgentCount = await _db.Agents.CountAsync(),
            ClientCount = await _db.Clients.CountAsync(),
            OpenInquiryCount = await _db.Inquiries.CountAsync(i => i.Status != InquiryStatus.Closed),
            ListedSaleValue = properties
                .Where(p => p.ListingType == ListingType.Sale && p.Status is ListingStatus.Available or ListingStatus.Pending)
                .Sum(p => p.Price),
            RecentProperties = properties.Take(5).Select(p => p.ToDto()).ToList()
        };
    }
}
