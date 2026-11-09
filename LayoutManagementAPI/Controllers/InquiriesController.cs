using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize(Roles = Roles.Staff)]
[ApiController]
[Route("api/inquiries")]
public class InquiriesController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public InquiriesController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<InquiryDto>>> GetAll([FromQuery] InquiryStatus? status)
    {
        var query = _db.Inquiries.Include(i => i.Property).Include(i => i.Client).AsQueryable();
        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        var inquiries = await query.OrderByDescending(i => i.CreatedOn).ToListAsync();
        return inquiries.Select(i => i.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InquiryDto>> Get(int id)
    {
        var inquiry = await _db.Inquiries.Include(i => i.Property).Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id);
        return inquiry is null ? NotFound() : inquiry.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<InquiryDto>> Create(InquiryRequest request)
    {
        if (!await IsValidAsync(request))
        {
            return ValidationProblem(ModelState);
        }

        var inquiry = new Inquiry { CreatedOn = DateTime.UtcNow };
        inquiry.Apply(request);
        _db.Inquiries.Add(inquiry);
        await _db.SaveChangesAsync();
        await LoadAsync(inquiry);
        return CreatedAtAction(nameof(Get), new { id = inquiry.Id }, inquiry.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InquiryDto>> Update(int id, InquiryRequest request)
    {
        var inquiry = await _db.Inquiries.Include(i => i.Property).Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inquiry is null)
        {
            return NotFound();
        }

        if (!await IsValidAsync(request))
        {
            return ValidationProblem(ModelState);
        }

        inquiry.Apply(request);
        await _db.SaveChangesAsync();
        await LoadAsync(inquiry);
        return inquiry.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var inquiry = await _db.Inquiries.FindAsync(id);
        if (inquiry is null)
        {
            return NotFound();
        }

        _db.Inquiries.Remove(inquiry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsValidAsync(InquiryRequest request)
    {
        if (!await _db.Properties.AnyAsync(p => p.Id == request.PropertyId))
        {
            ModelState.AddModelError(nameof(request.PropertyId), "Select a valid listing.");
        }

        var name = (User.Identity?.Name ?? "").ToLower();
        if (!await _db.Clients.AnyAsync(client => client.Id == request.ClientId && client.CreatedBy.ToLower() == name))
        {
            ModelState.AddModelError(nameof(request.ClientId), "Select a valid client.");
        }

        return ModelState.IsValid;
    }

    private async Task LoadAsync(Inquiry inquiry)
    {
        await _db.Entry(inquiry).Reference(i => i.Property).LoadAsync();
        await _db.Entry(inquiry).Reference(i => i.Client).LoadAsync();
    }
}
