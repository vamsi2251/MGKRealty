using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public NotificationsController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<RegistrationReminderDto>>> GetAll()
    {
        var name = User.Identity?.Name ?? "";
        var reminders = await _db.RegistrationReminders.AsNoTracking()
            .Where(reminder => reminder.UserName.ToLower() == name.ToLower())
            .OrderBy(reminder => reminder.ReadOn.HasValue)
            .ThenBy(reminder => reminder.RegistrationOn)
            .ThenBy(reminder => reminder.PlotNumber)
            .ToListAsync();
        return reminders.Select(ToDto).ToList();
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var name = User.Identity?.Name ?? "";
        var reminder = await _db.RegistrationReminders.FirstOrDefaultAsync(item => item.Id == id && item.UserName.ToLower() == name.ToLower());
        if (reminder is null)
        {
            return NotFound();
        }

        reminder.ReadOn ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static RegistrationReminderDto ToDto(Models.RegistrationReminder reminder) => new()
    {
        Id = reminder.Id,
        LayoutPlanId = reminder.LayoutPlanId,
        SketchName = reminder.SketchName,
        PlotNumber = reminder.PlotNumber,
        AdvanceReceived = reminder.AdvanceReceived,
        AdvanceReceivedOn = reminder.AdvanceReceivedOn,
        RegistrationOn = reminder.RegistrationOn,
        ReminderDate = reminder.ReminderDate,
        IsRead = reminder.ReadOn is not null
    };
}
