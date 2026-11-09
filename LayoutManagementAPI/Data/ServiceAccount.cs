namespace LayoutManagementAPI.Data;

/// <summary>
/// Optional hidden service account. Its username comes from the
/// "SuperAdmin:UserName" configuration value; there is no in-code default and
/// nothing is created at runtime.
/// </summary>
public static class ServiceAccount
{
    public static string? UserName(IConfiguration configuration)
    {
        var name = configuration["SuperAdmin:UserName"];
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
