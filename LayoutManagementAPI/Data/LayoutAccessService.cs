using System.Security.Claims;
using LayoutManagementAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Data;

public class LayoutAccessService
{
    private readonly RealEstateDbContext _db;

    public LayoutAccessService(RealEstateDbContext db) => _db = db;

    public bool IsSuper(ClaimsPrincipal user) => user.IsInRole(Roles.SuperAdmin);

    public bool IsStaff(ClaimsPrincipal user) =>
        IsSuper(user) || user.IsInRole(Roles.Admin);

    public static bool IsOwner(LayoutPlan layout, ClaimsPrincipal user) =>
        string.Equals(layout.UploadedBy, user.Identity?.Name, StringComparison.OrdinalIgnoreCase);

    public bool CanManage(LayoutPlan layout, ClaimsPrincipal user) =>
        IsSuper(user) || IsOwner(layout, user);

    public bool CanShare(LayoutPlan layout, ClaimsPrincipal user) =>
        CanManage(layout, user) || user.IsInRole(Roles.Admin);

    public async Task<bool> CanViewAsync(LayoutPlan layout, ClaimsPrincipal user)
    {
        if (IsSuper(user) || IsOwner(layout, user))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId)
            && await _db.LayoutAccesses.AnyAsync(access => access.LayoutPlanId == layout.Id && access.UserId == userId);
    }

    public async Task<bool> CanEditAsync(LayoutPlan layout, ClaimsPrincipal user)
    {
        if (IsSuper(user) || IsOwner(layout, user))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var level = await _db.LayoutAccesses
            .Where(access => access.LayoutPlanId == layout.Id && access.UserId == userId)
            .Select(access => (LayoutAccessLevel?)access.Level)
            .FirstOrDefaultAsync();
        return level == LayoutAccessLevel.Editable;
    }
}
