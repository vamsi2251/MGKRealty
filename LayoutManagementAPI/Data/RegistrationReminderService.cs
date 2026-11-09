using LayoutManagementAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Data;

public class RegistrationReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;

    public RegistrationReminderService(IServiceScopeFactory scopes) => _scopes = scopes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateDueRemindersAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CreateDueRemindersAsync(stoppingToken);
        }
    }

    private async Task CreateDueRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        var today = DateTime.Today;
        var plots = await db.Plots.Include(plot => plot.LayoutPlan)
            .Where(plot => plot.RegistrationOn != null && plot.LayoutPlan != null)
            .ToListAsync(cancellationToken);
        foreach (var plot in plots)
        {
            var registration = plot.RegistrationOn!.Value.Date;
            if (today >= registration)
            {
                continue;
            }

            for (var daysBefore = 1; daysBefore <= 4; daysBefore++)
            {
                var reminderDay = registration.AddDays(-daysBefore);
                if (reminderDay > today)
                {
                    continue;
                }

                var exists = await db.RegistrationReminders.AnyAsync(
                    reminder => reminder.PlotId == plot.Id && reminder.ReminderDate == reminderDay,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                db.RegistrationReminders.Add(new RegistrationReminder
                {
                    LayoutPlanId = plot.LayoutPlanId,
                    PlotId = plot.Id,
                    UserName = plot.LayoutPlan!.UploadedBy,
                    ReminderDate = reminderDay,
                    SketchName = plot.LayoutPlan.Name,
                    PlotNumber = plot.PlotNumber,
                    AdvanceReceived = plot.AdvanceReceived,
                    AdvanceReceivedOn = plot.AdvanceReceivedOn,
                    RegistrationOn = registration
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
