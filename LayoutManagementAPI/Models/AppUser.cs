using LayoutManagementAPI.Data;

namespace LayoutManagementAPI.Models;

/// <summary>
/// Application user for the custom auth / RBAC system (no ASP.NET Identity).
/// Id stays a string GUID so existing claims (NameIdentifier) and
/// <see cref="LayoutAccess.UserId"/> references keep working unchanged.
/// </summary>
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserName { get; set; } = "";

    /// <summary>Upper-cased username for case-insensitive, unique lookups.</summary>
    public string NormalizedUserName { get; set; } = "";

    public string Email { get; set; } = "";

    public string FullName { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    /// <summary>Single role per user. One of <see cref="Roles"/>.</summary>
    public string Role { get; set; } = Roles.User;

    public bool IsBuiltInAdmin { get; set; }

    /// <summary>When true the user cannot sign in (replaces Identity lockout).</summary>
    public bool LoginRestricted { get; set; }
}
