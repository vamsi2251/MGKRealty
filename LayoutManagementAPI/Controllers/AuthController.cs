using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LayoutManagementAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserAccountService _users;
    private readonly JwtOptions _jwt;

    public AuthController(UserAccountService users, IOptions<JwtOptions> jwt)
    {
        _users = users;
        _jwt = jwt.Value;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var username = request.Username.Trim();
        if (username.Length is < 1 or > 60)
        {
            return ValidationProblemFor(nameof(request.Username), "Enter a username.");
        }

        if (request.Password.Length < 8)
        {
            return ValidationProblemFor(nameof(request.Password), "The password must be at least 8 characters.");
        }

        if (await _users.UserNameTakenAsync(username))
        {
            return ValidationProblemFor(nameof(request.Username), "That username is already in use.");
        }

        // The very first account registered becomes the protected built-in admin; everyone else is a normal user.
        var isFirstUser = !await _users.AnyUsersAsync();
        var role = isFirstUser ? Roles.Admin : Roles.User;
        var user = await _users.CreateAsync(username, request.Email, request.FullName, request.Password, role, isBuiltInAdmin: isFirstUser);
        return CreateResponse(user);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _users.FindByNameAsync(request.Username.Trim());
        if (user is null || !_users.VerifyPassword(user, request.Password))
        {
            return Unauthorized("The username or password is incorrect.");
        }

        if (user.LoginRestricted)
        {
            return Unauthorized("This account cannot sign in. Ask an administrator.");
        }

        return CreateResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await _users.FindByIdAsync(id);
        return user is null ? Unauthorized() : CreateResponse(user);
    }

    private ActionResult ValidationProblemFor(string key, string message)
    {
        ModelState.AddModelError(key, message);
        return ValidationProblem(ModelState);
    }

    private AuthResponse CreateResponse(AppUser user)
    {
        var roles = new List<string> { user.Role };
        var expires = DateTime.UtcNow.AddHours(_jwt.AccessTokenHours);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("fullName", user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Issuer,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles,
            ExpiresOn = expires
        };
    }
}
