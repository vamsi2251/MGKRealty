using System.Text.RegularExpressions;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/plots")]
public class PlotsController : ControllerBase
{
    private static readonly Regex MobilePattern = new(@"^[0-9+\-\s]{10,15}$", RegexOptions.Compiled);
    private readonly RealEstateDbContext _db;
    private readonly LayoutAccessService _access;

    public PlotsController(RealEstateDbContext db, LayoutAccessService access)
    {
        _db = db;
        _access = access;
    }

    [HttpPut("{id:int}/booking")]
    public async Task<ActionResult<PlotDto>> UpdateBooking(int id, PlotBookingRequest request)
    {
        var (plot, denied) = await EditablePlotAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        var owner = plot!.LayoutPlan!.UploadedBy.ToLower();
        Agent? agent = null;
        Client? client = null;
        if (request.AgentId > 0)
        {
            agent = await _db.Agents.FirstOrDefaultAsync(item => item.Id == request.AgentId && item.CreatedBy.ToLower() == owner);
            if (agent is null)
            {
                ModelState.AddModelError(nameof(request.AgentId), "Select one of your agents.");
            }
        }

        if (request.ClientId > 0)
        {
            client = await _db.Clients.FirstOrDefaultAsync(item => item.Id == request.ClientId && item.CreatedBy.ToLower() == owner);
            if (client is null)
            {
                ModelState.AddModelError(nameof(request.ClientId), "Select one of your clients.");
            }
        }

        if (request.Area <= 0)
        {
            ModelState.AddModelError(nameof(request.Area), "Plot area must be greater than zero.");
        }

        if (request.AdvanceReceived > request.Price)
        {
            ModelState.AddModelError(nameof(request.AdvanceReceived), "Advance cannot be more than the plot price.");
        }

        if (request.AdvanceReceived > 0 || request.Status is PlotStatus.Blocked or PlotStatus.Sold)
        {
            if (client is null)
            {
                ModelState.AddModelError(nameof(request.ClientId), "Select a client to block or sell the plot.");
            }
            else if (string.IsNullOrWhiteSpace(client.Phone) || !MobilePattern.IsMatch(client.Phone))
            {
                ModelState.AddModelError(nameof(request.ClientId), "The selected client needs a mobile number with 10 to 15 digits.");
            }
        }

        if (request.Status == PlotStatus.Available && request.AdvanceReceived > 0)
        {
            ModelState.AddModelError(nameof(request.Status), "A plot with an advance is blocked, not available.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        plot.Area = request.Area;
        plot.Price = request.Price;
        plot.AdvanceReceived = request.AdvanceReceived;
        plot.AdvanceReceivedOn = request.AdvanceReceivedOn?.Date;
        plot.RegistrationOn = request.RegistrationOn?.Date;
        plot.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        plot.AgentId = agent?.Id;
        plot.ClientId = client?.Id;
        plot.ClientName = client?.Name;
        plot.ClientMobile = client?.Phone;
        if (request.Status == PlotStatus.Available && request.AdvanceReceived == 0)
        {
            plot.Status = PlotStatus.Available;
            plot.BlockedOn = null;
        }
        else
        {
            plot.Status = request.Status == PlotStatus.Sold ? PlotStatus.Sold : PlotStatus.Blocked;
            plot.BlockedOn ??= DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return await ToOwnedPlotAsync(plot);
    }

    [HttpPost("{id:int}/survey")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<PlotDto>> UploadSurvey(int id, IFormFile file)
    {
        var (plot, denied) = await EditablePlotAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Choose a JPG, JPEG, or PNG survey sketch.");
        }

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (!ImageProbe.TryRead(bytes, Path.GetExtension(file.FileName), out var info, out var error))
        {
            return BadRequest(error);
        }

        var survey = await _db.PlotSurveys.FirstOrDefaultAsync(item => item.PlotId == id);
        if (survey is null)
        {
            survey = new PlotSurvey { PlotId = id };
            _db.PlotSurveys.Add(survey);
        }

        survey.Content = bytes;
        survey.ContentType = info.ContentType;
        var fileName = Path.GetFileName(file.FileName);
        survey.FileName = fileName.Length <= 180 ? fileName : fileName[^180..];
        await _db.SaveChangesAsync();
        plot.Survey = survey;
        return await ToOwnedPlotAsync(plot);
    }

    [HttpGet("{id:int}/survey")]
    public async Task<IActionResult> Survey(int id)
    {
        var plot = await _db.Plots.Include(item => item.LayoutPlan).FirstOrDefaultAsync(item => item.Id == id);
        if (plot?.LayoutPlan is null || !await _access.CanViewAsync(plot.LayoutPlan, User))
        {
            return NotFound();
        }

        var survey = await _db.PlotSurveys.AsNoTracking().FirstOrDefaultAsync(item => item.PlotId == id);
        return survey is null ? NotFound() : File(survey.Content, survey.ContentType);
    }

    [HttpDelete("{id:int}/survey")]
    public async Task<IActionResult> DeleteSurvey(int id)
    {
        var (plot, denied) = await EditablePlotAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        var survey = await _db.PlotSurveys.FirstOrDefaultAsync(item => item.PlotId == id);
        if (survey is null)
        {
            return NoContent();
        }

        _db.PlotSurveys.Remove(survey);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(Plot? Plot, ActionResult? Denied)> EditablePlotAsync(int id)
    {
        var plot = await _db.Plots.Include(item => item.LayoutPlan).FirstOrDefaultAsync(item => item.Id == id);
        if (plot?.LayoutPlan is null || !await _access.CanViewAsync(plot.LayoutPlan, User))
        {
            return (null, NotFound());
        }

        return await _access.CanEditAsync(plot.LayoutPlan, User) ? (plot, null) : (null, Forbid());
    }

    private async Task<PlotDto> ToOwnedPlotAsync(Plot plot)
    {
        var dto = LayoutsController.ToPlot(plot, plot.LayoutPlan!.Unit);
        var survey = await _db.PlotSurveys.AsNoTracking()
            .Where(item => item.PlotId == plot.Id)
            .Select(item => item.FileName)
            .FirstOrDefaultAsync();
        dto.HasSurvey = survey is not null;
        dto.SurveyFileName = survey;
        if (plot.AgentId is int agentId)
        {
            dto.AgentName = await _db.Agents.AsNoTracking().Where(item => item.Id == agentId).Select(item => item.Name).FirstOrDefaultAsync();
        }

        return dto;
    }
}
