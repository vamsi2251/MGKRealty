using System.Security.Claims;
using LayoutManagementWebApp.Models;

namespace LayoutManagementWebApp.Services;

public class AuthSession
{
    public string? Token { get; private set; }
    public string Username { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public List<string> Roles { get; private set; } = new();
    public DateTime ExpiresOn { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token) && ExpiresOn > DateTime.UtcNow;

    public void Apply(StoredAuth auth)
    {
        Token = auth.Token;
        Username = auth.Username;
        FullName = auth.FullName;
        Roles = auth.Roles;
        ExpiresOn = auth.ExpiresOn;
    }

    public void Apply(AuthResponse auth) => Apply(new StoredAuth
    {
        Token = auth.Token,
        Username = auth.Username,
        FullName = auth.FullName,
        Email = auth.Email,
        Roles = auth.Roles,
        ExpiresOn = auth.ExpiresOn
    });

    public void Clear()
    {
        Token = null;
        Username = "";
        FullName = "";
        Roles = new();
        ExpiresOn = default;
    }

    public ClaimsPrincipal Principal()
    {
        if (!IsAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Username),
            new("fullName", FullName)
        };
        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }
}
