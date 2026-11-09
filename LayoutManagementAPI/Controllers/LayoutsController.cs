using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/layouts")]
public class LayoutsController : ControllerBase
{
    private readonly RealEstateDbContext _db;
    private readonly LayoutWorkflow _workflow;
    private readonly ILayoutExtractionService _extraction;
    private readonly LayoutAccessService _access;
    private readonly UserAccountService _users;
    private readonly IConfiguration _configuration;

    public LayoutsController(
        RealEstateDbContext db,
        LayoutWorkflow workflow,
        ILayoutExtractionService extraction,
        LayoutAccessService access,
        UserAccountService users,
        IConfiguration configuration)
    {
        _db = db;
        _workflow = workflow;
        _extraction = extraction;
        _access = access;
        _users = users;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<List<LayoutSummaryDto>>> GetAll()
    {
        var name = (User.Identity?.Name ?? "").ToLower();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var query = _db.LayoutPlans.Include(l => l.Plots).AsQueryable();
        if (!_access.IsSuper(User))
        {
            query = query.Where(layout =>
                layout.UploadedBy.ToLower() == name
                || layout.Accesses.Any(access => access.UserId == userId));
        }

        var layouts = await query.OrderByDescending(l => l.UploadedOn).ToListAsync();
        return layouts.Select(ToSummary).ToList();
    }

    [HttpGet("catalog")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<List<LayoutCatalogItemDto>>> Catalog()
    {
        var layouts = await _db.LayoutPlans.AsNoTracking()
            .OrderBy(layout => layout.Name)
            .Select(layout => new LayoutCatalogItemDto { Id = layout.Id, Name = layout.Name })
            .ToListAsync();
        return layouts;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LayoutDetailDto>> Get(int id)
    {
        var layout = await _db.LayoutPlans.Include(l => l.Plots).FirstOrDefaultAsync(l => l.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        var detail = ToDetail(layout);
        detail.HasImage = await _db.LayoutImages.AnyAsync(i => i.LayoutPlanId == id);
        await MarkSurveysAsync(detail);
        await FillFinanceAsync(detail);
        return await StampAsync(detail, layout);
    }

    [HttpPost("import")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<LayoutDetailDto>> Import(IFormFile file, [FromForm] string? name, [FromForm] string? mode, [FromForm] string? plots)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Choose a sketch image or JSON file.");
        }

        var uploadedBy = User.Identity?.Name ?? "unknown";
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        LayoutPlan plan;
        var jsonUpload = string.Equals(mode, "json", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
        if (jsonUpload)
        {
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Choose a layout document JSON file.");
            }

            string json;
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                json = await reader.ReadToEndAsync();
            }

            var importedJson = LayoutWorkflow.ImportJson(json, file.FileName, uploadedBy);
            if (importedJson.Plan is null)
            {
                return BadRequest(importedJson.Error);
            }

            plan = importedJson.Plan;
        }
        else if (extension is ".png" or ".jpg" or ".jpeg")
        {
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            var manual = string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase);
            List<ManualPlotInput> manualPlots = [];
            if (manual)
            {
                try
                {
                    manualPlots = string.IsNullOrWhiteSpace(plots)
                        ? []
                        : JsonSerializer.Deserialize<List<ManualPlotInput>>(plots, LayoutExtractionService.JsonOptions) ?? [];
                }
                catch (JsonException)
                {
                    return BadRequest("The manual plot table could not be read.");
                }
            }

            var imported = manual
                ? _workflow.ImportManual(bytes, extension, file.FileName, uploadedBy, manualPlots)
                : await _workflow.ImportImageAsync(bytes, extension, file.FileName, uploadedBy, HttpContext.RequestAborted);
            if (imported.Plan is null)
            {
                return BadRequest(imported.Error);
            }

            plan = imported.Plan;
        }
        else
        {
            return BadRequest("Upload a JPG, JPEG, PNG, or JSON sketch.");
        }

        var nameError = ApplyLayoutName(plan, name);
        if (nameError is not null)
        {
            return BadRequest(nameError);
        }

        _db.LayoutPlans.Add(plan);
        await _db.SaveChangesAsync();
        var detail = ToDetail(plan);
        detail.HasImage = plan.Image is not null;
        detail.Document = LayoutWorkflow.ReadDocument(plan);
        await MarkSurveysAsync(detail);
        await FillFinanceAsync(detail);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, await StampAsync(detail, plan));
    }

    [HttpPost("{id:int}/extract")]
    public async Task<ActionResult<LayoutDetailDto>> Extract(int id, CancellationToken cancellationToken)
    {
        var layout = await _db.LayoutPlans.Include(item => item.Plots).ThenInclude(plot => plot.Survey).Include(item => item.Image).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (layout?.Image is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        if (!await _access.CanEditAsync(layout, User))
        {
            return Forbid();
        }

        var document = await _extraction.ExtractAsync(
            new LayoutExtractionRequest(layout.Image.Content, layout.Image.ContentType, 0, 0, layout.FileName),
            cancellationToken);
        if (string.Equals(document.Extraction.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            var reason = string.Join(" ", document.Extraction.Warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
            return BadRequest(string.IsNullOrWhiteSpace(reason) ? "ChatGPT extraction failed." : reason);
        }

        if (!ImageProbe.TryRead(layout.Image.Content, Path.GetExtension(layout.FileName), out var info, out _))
        {
            info = new ImageInfo(document.CanvasWidth, document.CanvasHeight, layout.Image.ContentType);
        }

        document.CanvasWidth = info.Width;
        document.CanvasHeight = info.Height;
        document.Name = layout.Name;
        LayoutWorkflow.ApplyExtractedAreas(layout, document);

        await _db.SaveChangesAsync(cancellationToken);
        var detail = ToDetail(layout);
        detail.HasImage = true;
        detail.Document = LayoutWorkflow.ReadDocument(layout);
        await MarkSurveysAsync(detail, cancellationToken);
        await FillFinanceAsync(detail);
        return await StampAsync(detail, layout);
    }

    [HttpPut("{id:int}/document")]
    public async Task<ActionResult<LayoutDetailDto>> SaveDocument(int id, LayoutDocument document, CancellationToken cancellationToken)
    {
        var layout = await _db.LayoutPlans.Include(item => item.Plots).ThenInclude(plot => plot.Survey).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        var deniedDocument = await GuardEditAsync(layout);
        if (deniedDocument is not null)
        {
            return deniedDocument;
        }

        var error = LayoutWorkflow.SaveDocument(layout!, document, out var report);
        if (error is not null)
        {
            return BadRequest(new { errors = report.Errors, warnings = report.Warnings });
        }

        await _db.SaveChangesAsync(cancellationToken);
        var detail = ToDetail(layout);
        detail.HasImage = await _db.LayoutImages.AnyAsync(item => item.LayoutPlanId == id, cancellationToken);
        detail.Document = document;
        await MarkSurveysAsync(detail, cancellationToken);
        await FillFinanceAsync(detail);
        return await StampAsync(detail, layout!);
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> Image(int id)
    {
        var layout = await _db.LayoutPlans.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        var image = await _db.LayoutImages.AsNoTracking().FirstOrDefaultAsync(i => i.LayoutPlanId == id);
        return image is null ? NotFound() : File(image.Content, image.ContentType);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<LayoutDetailDto>> Complete(int id, CancellationToken cancellationToken)
    {
        var layout = await _db.LayoutPlans.Include(item => item.Plots).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        if (!_access.CanManage(layout, User))
        {
            return Forbid();
        }

        if (layout.Plots.Count == 0 || layout.Plots.Any(plot => plot.Status != PlotStatus.Sold))
        {
            return BadRequest("Every plot must be sold before this layout can be completed.");
        }

        layout.CompletedOn ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        var detail = ToDetail(layout);
        detail.HasImage = await _db.LayoutImages.AnyAsync(item => item.LayoutPlanId == id, cancellationToken);
        await MarkSurveysAsync(detail, cancellationToken);
        await FillFinanceAsync(detail);
        return await StampAsync(detail, layout);
    }

    [HttpPut("{id:int}/owner")]
    public async Task<ActionResult<LayoutDetailDto>> SaveOwnerPayable(int id, OwnerPayableRequest request)
    {
        var (layout, denied) = await EditableAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        var paid = await OwnerPaidAsync(id);
        if (request.OwnerPayable < paid)
        {
            return BadRequest("The amount payable to the land owner cannot be less than the payments already recorded.");
        }

        layout.OwnerPayable = request.OwnerPayable;
        await _db.SaveChangesAsync();
        return await DetailAsync(layout);
    }

    [HttpPost("{id:int}/owner-payments")]
    public async Task<ActionResult<LayoutDetailDto>> AddOwnerPayment(int id, OwnerPaymentRequest request)
    {
        var (layout, denied) = await EditableAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        if (request.PaidOn == default)
        {
            return BadRequest("Choose the date the owner was paid.");
        }

        var paid = await OwnerPaidAsync(id);
        if (paid + request.Amount > layout.OwnerPayable)
        {
            return BadRequest("That payment is more than the amount still owed to the land owner.");
        }

        _db.OwnerPayments.Add(new OwnerPayment { LayoutPlanId = id, PaidOn = request.PaidOn.Date, Amount = request.Amount });
        await _db.SaveChangesAsync();
        return await DetailAsync(layout);
    }

    [HttpPut("{id:int}/owner-payments/{paymentId:int}")]
    public async Task<ActionResult<LayoutDetailDto>> UpdateOwnerPayment(int id, int paymentId, OwnerPaymentRequest request)
    {
        var (layout, denied) = await EditableAsync(id);
        var payment = layout is null ? null : await _db.OwnerPayments.FirstOrDefaultAsync(item => item.Id == paymentId && item.LayoutPlanId == id);
        if (denied is not null)
        {
            return denied;
        }

        if (payment is null)
        {
            return NotFound();
        }

        if (request.PaidOn == default)
        {
            return BadRequest("Choose the date the owner was paid.");
        }

        var paid = await OwnerPaidAsync(id, paymentId);
        if (paid + request.Amount > layout.OwnerPayable)
        {
            return BadRequest("That payment is more than the amount still owed to the land owner.");
        }

        payment.PaidOn = request.PaidOn.Date;
        payment.Amount = request.Amount;
        await _db.SaveChangesAsync();
        return await DetailAsync(layout);
    }

    [HttpDelete("{id:int}/owner-payments/{paymentId:int}")]
    public async Task<ActionResult<LayoutDetailDto>> DeleteOwnerPayment(int id, int paymentId)
    {
        var (layout, denied) = await EditableAsync(id);
        var payment = layout is null ? null : await _db.OwnerPayments.FirstOrDefaultAsync(item => item.Id == paymentId && item.LayoutPlanId == id);
        if (denied is not null)
        {
            return denied;
        }

        if (payment is null)
        {
            return NotFound();
        }

        _db.OwnerPayments.Remove(payment);
        await _db.SaveChangesAsync();
        return await DetailAsync(layout);
    }

    [HttpPut("{id:int}/expenses")]
    public async Task<ActionResult<LayoutDetailDto>> SaveExpenses(int id, DevelopmentExpenseRequest request)
    {
        var (layout, denied) = await EditableAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        layout!.SurveyorAmount = request.SurveyorAmount;
        layout.TipperCount = request.TipperCount;
        layout.TipperCost = request.TipperCost;
        layout.MiscellaneousAmount = request.MiscellaneousAmount;
        await _db.SaveChangesAsync();
        return await DetailAsync(layout);
    }

    [HttpPost("{id:int}/plots")]
    public async Task<ActionResult<PlotDto>> AddPlot(int id)
    {
        var (layout, denied) = await EditableAsync(id);
        if (denied is not null)
        {
            return denied;
        }

        var next = layout!.Plots
            .Select(plot => int.TryParse(plot.PlotNumber, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var plot = new Plot
        {
            PlotNumber = next.ToString(),
            Area = 0,
            Status = PlotStatus.Available,
            VerticesJson = "[]"
        };
        layout.Plots.Add(plot);
        var document = LayoutWorkflow.ReadDocument(layout);
        document.Coordinates.AreaUnit = "cent";
        document.Plots.Add(new DocumentPlot
        {
            Id = $"plot-{next}",
            PlotNumber = next.ToString(),
            Status = "available",
            Area = new MeasuredValue { Value = null, Unit = "cent", Source = "manual" },
            Vertices =
            [
                new() { X = 0, Y = 0 },
                new() { X = 0.001, Y = 0 },
                new() { X = 0, Y = 0.001 }
            ]
        });
        layout.Unit = "cent";
        layout.DocumentJson = System.Text.Json.JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions);
        await _db.SaveChangesAsync();
        return ToPlot(plot, layout.Unit);
    }

    [HttpGet("{id:int}/partner-users")]
    public async Task<ActionResult<List<UserDto>>> PartnerUsers(int id)
    {
        var layout = await _db.LayoutPlans.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User) || !_access.CanShare(layout, User))
        {
            return NotFound();
        }

        var hidden = ServiceAccount.UserName(_configuration);
        var users = await _users.Query().OrderBy(user => user.FullName).ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            if (!string.IsNullOrWhiteSpace(hidden) && string.Equals(user.UserName, hidden, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(user.UserName, layout.UploadedBy, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new UserDto
            {
                Id = user.Id,
                Username = user.UserName ?? "",
                FullName = user.FullName,
                Email = user.Email ?? ""
            });
        }

        return result;
    }

    [HttpGet("{id:int}/partners")]
    public async Task<ActionResult<List<LayoutPartnerDto>>> Partners(int id)
    {
        var layout = await _db.LayoutPlans.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User) || !_access.CanShare(layout, User))
        {
            return NotFound();
        }

        return await PartnerListAsync(id);
    }

    [HttpPost("{id:int}/partners")]
    public async Task<ActionResult<List<LayoutPartnerDto>>> AddPartner(int id, LayoutPartnerRequest request)
    {
        var layout = await _db.LayoutPlans.FirstOrDefaultAsync(item => item.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        if (!_access.CanShare(layout, User))
        {
            return Forbid();
        }

        if (request.Level is not (LayoutAccessLevel.ReadOnly or LayoutAccessLevel.Editable))
        {
            return BadRequest("Choose read only or editable access.");
        }

        var user = await _users.FindByIdAsync(request.UserId);
        var hidden = ServiceAccount.UserName(_configuration);
        if (user is null || (!string.IsNullOrWhiteSpace(hidden) && user.UserName!.Equals(hidden, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("Choose a user account.");
        }

        if (user.UserName!.Equals(layout.UploadedBy, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("The person who uploaded this sketch already has it.");
        }

        var existing = await _db.LayoutAccesses.FirstOrDefaultAsync(access => access.LayoutPlanId == id && access.UserId == user.Id);
        if (existing is null)
        {
            _db.LayoutAccesses.Add(new LayoutAccess { LayoutPlanId = id, UserId = user.Id, Level = request.Level });
        }
        else
        {
            existing.Level = request.Level;
        }

        await _db.SaveChangesAsync();
        return await PartnerListAsync(id);
    }

    [HttpDelete("{id:int}/partners/{userId}")]
    public async Task<ActionResult<List<LayoutPartnerDto>>> RemovePartner(int id, string userId)
    {
        var layout = await _db.LayoutPlans.FirstOrDefaultAsync(item => item.Id == id);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        if (!_access.CanShare(layout, User))
        {
            return Forbid();
        }

        var existing = await _db.LayoutAccesses.FirstOrDefaultAsync(access => access.LayoutPlanId == id && access.UserId == userId);
        if (existing is not null)
        {
            _db.LayoutAccesses.Remove(existing);
            await _db.SaveChangesAsync();
        }

        return await PartnerListAsync(id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var layout = await _db.LayoutPlans.FindAsync(id);
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        if (!_access.CanManage(layout, User))
        {
            return Forbid();
        }

        _db.LayoutPlans.Remove(layout);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> GuardEditAsync(LayoutPlan? layout)
    {
        if (layout is null || !await _access.CanViewAsync(layout, User))
        {
            return NotFound();
        }

        return await _access.CanEditAsync(layout, User) ? null : Forbid();
    }

    private async Task<(LayoutPlan? Layout, ActionResult? Denied)> EditableAsync(int id)
    {
        var layout = await _db.LayoutPlans.Include(item => item.Plots).FirstOrDefaultAsync(item => item.Id == id);
        var denied = await GuardEditAsync(layout);
        return (denied is null ? layout : null, denied);
    }

    private async Task<LayoutDetailDto> StampAsync(LayoutDetailDto detail, LayoutPlan layout)
    {
        detail.CanEdit = await _access.CanEditAsync(layout, User);
        detail.CanManage = _access.CanManage(layout, User);
        detail.CanShare = _access.CanShare(layout, User);
        return detail;
    }

    private async Task<List<LayoutPartnerDto>> PartnerListAsync(int layoutId)
    {
        var rows = await _db.LayoutAccesses.AsNoTracking().Where(access => access.LayoutPlanId == layoutId).ToListAsync();
        var result = new List<LayoutPartnerDto>();
        foreach (var row in rows)
        {
            var user = await _users.FindByIdAsync(row.UserId);
            if (user is null)
            {
                continue;
            }

            result.Add(new LayoutPartnerDto
            {
                UserId = user.Id,
                Username = user.UserName ?? "",
                FullName = user.FullName,
                Level = row.Level
            });
        }

        return result.OrderBy(partner => partner.FullName).ToList();
    }

    private async Task<LayoutDetailDto> DetailAsync(LayoutPlan layout)
    {
        var detail = ToDetail(layout);
        detail.HasImage = await _db.LayoutImages.AnyAsync(item => item.LayoutPlanId == layout.Id);
        detail.Document = LayoutWorkflow.ReadDocument(layout);
        await MarkSurveysAsync(detail);
        await FillFinanceAsync(detail);
        return await StampAsync(detail, layout);
    }

    private async Task FillFinanceAsync(LayoutDetailDto detail)
    {
        var layout = await _db.LayoutPlans.AsNoTracking().FirstAsync(item => item.Id == detail.Id);
        var payments = await _db.OwnerPayments.AsNoTracking()
            .Where(payment => payment.LayoutPlanId == detail.Id)
            .OrderByDescending(payment => payment.PaidOn)
            .ThenByDescending(payment => payment.Id)
            .ToListAsync();
        var paid = payments.Sum(payment => payment.Amount);
        var tipperTotal = layout.TipperCount * layout.TipperCost;
        detail.OwnerPayable = layout.OwnerPayable;
        detail.OwnerPaid = paid;
        detail.OwnerRemaining = layout.OwnerPayable - paid;
        detail.OwnerPayments = payments.Select(payment => new OwnerPaymentDto { Id = payment.Id, PaidOn = payment.PaidOn, Amount = payment.Amount }).ToList();
        detail.SurveyorAmount = layout.SurveyorAmount;
        detail.TipperCount = layout.TipperCount;
        detail.TipperCost = layout.TipperCost;
        detail.TipperTotal = tipperTotal;
        detail.MiscellaneousAmount = layout.MiscellaneousAmount;
        detail.DevelopmentTotal = layout.SurveyorAmount + tipperTotal + layout.MiscellaneousAmount;
    }

    private static string? ApplyLayoutName(LayoutPlan plan, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 120)
        {
            return "Layout name must be 120 characters or fewer.";
        }

        plan.Name = trimmed;
        if (string.IsNullOrWhiteSpace(plan.DocumentJson))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<LayoutDocument>(plan.DocumentJson, LayoutExtractionService.JsonOptions);
        if (document is null)
        {
            return null;
        }

        document.Name = trimmed;
        plan.DocumentJson = JsonSerializer.Serialize(document, LayoutExtractionService.JsonOptions);
        return null;
    }

    private static LayoutSummaryDto ToSummary(LayoutPlan layout) => new()
    {
        Id = layout.Id,
        Name = layout.Name,
        Unit = layout.Unit,
        FileName = layout.FileName,
        UploadedBy = layout.UploadedBy,
        UploadedOn = layout.UploadedOn,
        CompletedOn = layout.CompletedOn,
        PlotCount = layout.Plots.Count,
        TotalArea = layout.Plots.Sum(p => p.Area),
        AvailableCount = layout.Plots.Count(p => p.Status == PlotStatus.Available),
        BlockedCount = layout.Plots.Count(p => p.Status == PlotStatus.Blocked),
        AdvanceTotal = layout.Plots.Sum(p => p.AdvanceReceived)
    };

    internal static LayoutDetailDto ToDetail(LayoutPlan layout)
    {
        var plots = layout.Plots
            .OrderBy(p => int.TryParse(p.PlotNumber, out var number) ? number : int.MaxValue)
            .ThenBy(p => p.PlotNumber)
            .Select(p => ToPlot(p, layout.Unit))
            .ToList();
        return new LayoutDetailDto
        {
            Id = layout.Id,
            Name = layout.Name,
            Unit = layout.Unit,
            Notes = layout.Notes,
            FileName = layout.FileName,
            UploadedBy = layout.UploadedBy,
            UploadedOn = layout.UploadedOn,
            CompletedOn = layout.CompletedOn,
            PlotCount = plots.Count,
            TotalArea = plots.Sum(p => p.Area),
            Plots = plots,
            Document = LayoutWorkflow.ReadDocument(layout)
        };
    }

    private async Task<decimal> OwnerPaidAsync(int layoutId, int? exceptPaymentId = null)
    {
        var payments = _db.OwnerPayments.Where(payment => payment.LayoutPlanId == layoutId);
        if (exceptPaymentId is int skipped)
        {
            payments = payments.Where(payment => payment.Id != skipped);
        }

        var amounts = await payments.Select(payment => payment.Amount).ToListAsync();
        return amounts.Sum();
    }

    internal static PlotDto ToPlot(Plot plot, string unit) => new()
    {
        Id = plot.Id,
        PlotNumber = plot.PlotNumber,
        Area = plot.Area,
        LengthFeet = plot.LengthFeet,
        BreadthFeet = plot.BreadthFeet,
        TopFeet = plot.TopFeet,
        RightFeet = plot.RightFeet,
        BottomFeet = plot.BottomFeet,
        LeftFeet = plot.LeftFeet,
        Unit = unit,
        Facing = plot.Facing,
        Price = plot.Price,
        Status = plot.Status,
        AgentId = plot.AgentId ?? 0,
        AgentName = null,
        ClientId = plot.ClientId ?? 0,
        ClientName = plot.ClientName,
        ClientMobile = plot.ClientMobile,
        AdvanceReceived = plot.AdvanceReceived,
        AdvanceReceivedOn = plot.AdvanceReceivedOn,
        RegistrationOn = plot.RegistrationOn,
        Balance = plot.Price - plot.AdvanceReceived,
        BlockedOn = plot.BlockedOn,
        Comment = plot.Comment,
        HasSurvey = plot.Survey is not null,
        SurveyFileName = plot.Survey?.FileName,
        Vertices = SketchParser.ReadVertices(plot.VerticesJson)
    };

    private async Task MarkSurveysAsync(LayoutDetailDto detail, CancellationToken cancellationToken = default)
    {
        var ids = detail.Plots.Select(plot => plot.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var surveys = await _db.PlotSurveys.AsNoTracking()
            .Where(survey => ids.Contains(survey.PlotId))
            .Select(survey => new { survey.PlotId, survey.FileName })
            .ToListAsync(cancellationToken);
        foreach (var plot in detail.Plots)
        {
            var survey = surveys.FirstOrDefault(item => item.PlotId == plot.Id);
            plot.HasSurvey = survey is not null;
            plot.SurveyFileName = survey?.FileName;
        }

        var agentIds = detail.Plots.Where(plot => plot.AgentId > 0).Select(plot => plot.AgentId).Distinct().ToList();
        if (agentIds.Count > 0)
        {
            var agents = await _db.Agents.AsNoTracking()
                .Where(agent => agentIds.Contains(agent.Id))
                .Select(agent => new { agent.Id, agent.Name })
                .ToListAsync(cancellationToken);
            foreach (var plot in detail.Plots)
            {
                plot.AgentName = agents.FirstOrDefault(agent => agent.Id == plot.AgentId)?.Name;
            }
        }
    }
}
