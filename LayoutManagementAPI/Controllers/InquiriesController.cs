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
    public async Task<ActionResult<List<InquiryDto>>> GetAll([FromQuery] int? layoutId)
    {
        var query = _db.Inquiries.Include(i => i.LayoutPlan).AsQueryable();
        if (layoutId is not null)
        {
            query = query.Where(i => i.LayoutPlanId == layoutId);
        }

        var inquiries = await query.OrderByDescending(i => i.CreatedOn).ToListAsync();
        return inquiries.Select(i => i.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InquiryDto>> Get(int id)
    {
        var inquiry = await _db.Inquiries.Include(i => i.LayoutPlan)
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
        var inquiry = await _db.Inquiries.Include(i => i.LayoutPlan)
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
        if (request.LayoutPlanId is not null
            && !await _db.LayoutPlans.AnyAsync(l => l.Id == request.LayoutPlanId))
        {
            ModelState.AddModelError(nameof(request.LayoutPlanId), "Select a valid sketch.");
        }

        return ModelState.IsValid;
    }

    private async Task LoadAsync(Inquiry inquiry)
    {
        await _db.Entry(inquiry).Reference(i => i.LayoutPlan).LoadAsync();
    }
}
