namespace LayoutManagementAPI.Data;

/// <summary>
/// Role names used by the custom role-based access control.
/// A user holds exactly one of these in <see cref="Models.AppUser.Role"/>.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Comma-separated staff roles, for use in [Authorize(Roles = ...)].</summary>
    public const string Staff = Admin + "," + SuperAdmin;

    public static readonly string[] All = { Admin, User, SuperAdmin };

    public static bool IsValid(string role) => Array.Exists(All, r => r == role);
}
