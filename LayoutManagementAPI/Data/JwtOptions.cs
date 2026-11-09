using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Data;

/// <summary>
/// Strongly-typed JWT settings bound from the "Jwt" configuration section.
/// All values come from configuration — there are no in-code defaults.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    [Range(1, 168)]
    public int AccessTokenHours { get; set; }
}
