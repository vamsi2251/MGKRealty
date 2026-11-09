using System.Security.Claims;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserAccountService _users;
    private readonly RealEstateDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly LayoutAccessService _access;

    public UsersController(UserAccountService users, RealEstateDbContext db, IConfiguration configuration, LayoutAccessService access)
    {
        _users = users;
        _db = db;
        _configuration = configuration;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var hidden = ServiceAccount.UserName(_configuration);
        var users = await _users.Query().OrderBy(u => u.UserName).ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            if (IsServiceAccount(user, hidden))
            {
                continue;
            }

            result.Add(ToDto(user));
        }

        return result;
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
    {
        var hidden = ServiceAccount.UserName(_configuration);
        var username = request.Username.Trim();
        if (!string.IsNullOrWhiteSpace(hidden)
            && username.Equals(hidden, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("That username is reserved.");
        }

        if (username.Length is < 1 or > 60)
        {
            return BadRequest("Enter a username.");
        }

        if (request.Password.Length < 8)
        {
            return BadRequest("The password must be at least 8 characters.");
        }

        if (await _users.UserNameTakenAsync(username))
        {
            return Conflict("That username is already in use.");
        }

        var user = await _users.CreateAsync(username, request.Email, request.FullName, request.Password, Roles.User);

        var assigned = await ReplaceSketchesAsync(user, request.Sketches);
        if (assigned is not null)
        {
            return assigned;
        }

        return ToDto(user);
    }

    [HttpGet("{id}/sketches")]
    public async Task<ActionResult<List<UserSketchGrant>>> Sketches(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null || IsServiceAccount(user, ServiceAccount.UserName(_configuration)))
        {
            return NotFound();
        }

        var rows = await _db.LayoutAccesses.AsNoTracking()
            .Where(access => access.UserId == user.Id)
            .Select(access => new UserSketchGrant { LayoutId = access.LayoutPlanId, Level = access.Level })
            .ToListAsync();
        return rows;
    }

    [HttpPut("{id}/sketches")]
    public async Task<IActionResult> SetSketches(string id, UserSketchAccessRequest request)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null || IsServiceAccount(user, ServiceAccount.UserName(_configuration)))
        {
            return NotFound();
        }

        var assigned = await ReplaceSketchesAsync(user, request.Sketches);
        return assigned ?? NoContent();
    }

    [HttpPut("{id}/credentials")]
    public async Task<ActionResult<UserDto>> SetCredentials(string id, AccountCredentialsRequest request)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null || IsServiceAccount(user, ServiceAccount.UserName(_configuration)))
        {
            return NotFound();
        }

        var username = request.Username.Trim();
        if (username.Length is < 1 or > 60)
        {
            return BadRequest("Enter a username.");
        }

        var hidden = ServiceAccount.UserName(_configuration);
        if (!string.IsNullOrWhiteSpace(hidden) && username.Equals(hidden, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("That username is reserved.");
        }

        if (await _users.UserNameTakenAsync(username, user.Id))
        {
            return Conflict("That username is already in use.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword) && request.NewPassword.Length < 8)
        {
            return BadRequest("A new password must be at least 8 characters.");
        }

        var previousName = user.UserName;
        if (!username.Equals(previousName, StringComparison.OrdinalIgnoreCase))
        {
            await _users.SetUserNameAsync(user, username);
            await RenameOwnerAsync(previousName, username);
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            await _users.SetPasswordAsync(user, request.NewPassword);
        }

        return ToDto(user);
    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> SetRole(string id, RoleUpdateRequest request)
    {
        if (request.Role is not (Roles.Admin or Roles.User))
        {
            return BadRequest("Role must be Admin or User.");
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (IsServiceAccount(user, ServiceAccount.UserName(_configuration)))
        {
            return Conflict("The service account cannot be changed.");
        }

        if (user.IsBuiltInAdmin && request.Role != Roles.Admin)
        {
            return Conflict("The built-in admin account must stay an administrator.");
        }

        await _users.SetRoleAsync(user, request.Role);
        return ToDto(user);
    }

    [HttpPut("{id}/login")]
    public async Task<ActionResult<UserDto>> SetLoginAccess(string id, LoginAccessRequest request)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (IsServiceAccount(user, ServiceAccount.UserName(_configuration)))
        {
            return Conflict("The service account cannot be changed.");
        }

        if (user.IsBuiltInAdmin && !request.Allowed)
        {
            return Conflict("The built-in admin account cannot be restricted.");
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal) && !request.Allowed)
        {
            return Conflict("You cannot restrict your own sign-in.");
        }

        await _users.SetLoginRestrictedAsync(user, !request.Allowed);
        return ToDto(user);
    }

    private async Task RenameOwnerAsync(string previousName, string username)
    {
        var plans = await _db.LayoutPlans.Where(layout => layout.UploadedBy.ToLower() == previousName.ToLower()).ToListAsync();
        foreach (var plan in plans)
        {
            plan.UploadedBy = username;
        }

        var agents = await _db.Agents.Where(agent => agent.CreatedBy.ToLower() == previousName.ToLower()).ToListAsync();
        foreach (var agent in agents)
        {
            agent.CreatedBy = username;
        }

        var clients = await _db.Clients.Where(client => client.CreatedBy.ToLower() == previousName.ToLower()).ToListAsync();
        foreach (var client in clients)
        {
            client.CreatedBy = username;
        }

        var reminders = await _db.RegistrationReminders.Where(reminder => reminder.UserName.ToLower() == previousName.ToLower()).ToListAsync();
        foreach (var reminder in reminders)
        {
            reminder.UserName = username;
        }

        await _db.SaveChangesAsync();
    }

    private bool IsServiceAccount(AppUser user, string? hidden) =>
        !string.IsNullOrWhiteSpace(hidden)
        && string.Equals(user.UserName, hidden, StringComparison.OrdinalIgnoreCase);

    private async Task<ActionResult?> ReplaceSketchesAsync(AppUser user, List<UserSketchGrant> sketches)
    {
        var wanted = sketches
            .GroupBy(sketch => sketch.LayoutId)
            .Select(group => group.Last())
            .ToList();
        if (wanted.Any(sketch => sketch.Level is not (LayoutAccessLevel.ReadOnly or LayoutAccessLevel.Editable)))
        {
            return BadRequest("Sketch access must be read only or editable.");
        }

        var layouts = await _db.LayoutPlans.Where(layout => wanted.Select(sketch => sketch.LayoutId).Contains(layout.Id)).ToListAsync();
        if (layouts.Count != wanted.Count)
        {
            return BadRequest("Choose sketches that exist.");
        }

        if (layouts.Any(layout => !_access.CanShare(layout, User)))
        {
            return Forbid();
        }

        if (layouts.Any(layout => layout.UploadedBy.Equals(user.UserName, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("The person who uploaded a sketch already has that sketch.");
        }

        var manageable = _db.LayoutPlans.AsQueryable();
        if (!_access.IsStaff(User))
        {
            var name = (User.Identity?.Name ?? "").ToLower();
            manageable = manageable.Where(layout => layout.UploadedBy.ToLower() == name);
        }

        var manageableIds = await manageable.Select(layout => layout.Id).ToListAsync();
        var existing = await _db.LayoutAccesses.Where(access => access.UserId == user.Id && manageableIds.Contains(access.LayoutPlanId)).ToListAsync();
        _db.LayoutAccesses.RemoveRange(existing);
        foreach (var sketch in wanted)
        {
            _db.LayoutAccesses.Add(new LayoutAccess
            {
                LayoutPlanId = sketch.LayoutId,
                UserId = user.Id,
                Level = sketch.Level
            });
        }

        await _db.SaveChangesAsync();
        return null;
    }

    private UserDto ToDto(AppUser user) => new()
    {
        Id = user.Id,
        Username = user.UserName,
        FullName = user.FullName,
        Email = user.Email,
        Roles = new List<string> { user.Role },
        LoginRestricted = user.LoginRestricted,
        IsBuiltInAdmin = user.IsBuiltInAdmin
    };
}
