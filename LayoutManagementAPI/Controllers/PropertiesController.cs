using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize(Roles = Roles.Staff)]
[ApiController]
[Route("api/properties")]
public class PropertiesController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public PropertiesController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<PropertyDto>>> GetAll(
        [FromQuery] ListingStatus? status,
        [FromQuery] ListingType? listingType,
        [FromQuery] string? city)
    {
        var query = _db.Properties.Include(p => p.Agent).AsQueryable();
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (listingType is not null)
        {
            query = query.Where(p => p.ListingType == listingType);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var term = city.Trim();
            query = query.Where(p => p.City.Contains(term));
        }

        var properties = await query.OrderByDescending(p => p.ListedOn).ToListAsync();
        return properties.Select(p => p.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PropertyDto>> Get(int id)
    {
        var property = await _db.Properties.Include(p => p.Agent).FirstOrDefaultAsync(p => p.Id == id);
        return property is null ? NotFound() : property.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(PropertyRequest request)
    {
        if (!await OwnsAgentAsync(request.AgentId))
        {
            ModelState.AddModelError(nameof(request.AgentId), "Select a valid agent.");
            return ValidationProblem(ModelState);
        }

        var property = new PropertyListing { ListedOn = DateTime.UtcNow };
        property.Apply(request);
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        await _db.Entry(property).Reference(p => p.Agent).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = property.Id }, property.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PropertyDto>> Update(int id, PropertyRequest request)
    {
        var property = await _db.Properties.Include(p => p.Agent).FirstOrDefaultAsync(p => p.Id == id);
        if (property is null)
        {
            return NotFound();
        }

        if (!await OwnsAgentAsync(request.AgentId))
        {
            ModelState.AddModelError(nameof(request.AgentId), "Select a valid agent.");
            return ValidationProblem(ModelState);
        }

        property.Apply(request);
        await _db.SaveChangesAsync();
        await _db.Entry(property).Reference(p => p.Agent).LoadAsync();
        return property.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        _db.Properties.Remove(property);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Task<bool> OwnsAgentAsync(int agentId)
    {
        var name = (User.Identity?.Name ?? "").ToLower();
        return _db.Agents.AnyAsync(agent => agent.Id == agentId && agent.CreatedBy.ToLower() == name);
    }
}
