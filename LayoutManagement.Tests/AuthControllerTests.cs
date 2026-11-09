using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Controllers;
using LayoutManagementAPI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayoutManagement.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly TestDatabase _test = new();
    private readonly UserAccountService _users;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _users = new UserAccountService(_test.Db);
        var jwt = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Key = "unit-test-signing-key-that-is-long-enough-123456",
            AccessTokenHours = 8
        });
        _controller = new AuthController(_users, jwt)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _test.Dispose();

    private static RegisterRequest Reg(string username) => new()
    {
        Username = username,
        Email = $"{username}@example.com",
        FullName = username,
        Password = "Password1!"
    };

    [Fact]
    public async Task First_registered_user_becomes_builtin_admin()
    {
        var result = await _controller.Register(Reg("first"));

        var response = result.Value;
        Assert.NotNull(response);
        Assert.Equal(Roles.Admin, Assert.Single(response!.Roles));

        var stored = await _users.FindByNameAsync("first");
        Assert.True(stored!.IsBuiltInAdmin);
        Assert.Equal(Roles.Admin, stored.Role);
    }

    [Fact]
    public async Task Second_registered_user_is_a_normal_user()
    {
        await _controller.Register(Reg("first"));

        var result = await _controller.Register(Reg("second"));

        var response = result.Value;
        Assert.NotNull(response);
        Assert.Equal(Roles.User, Assert.Single(response!.Roles));

        var stored = await _users.FindByNameAsync("second");
        Assert.False(stored!.IsBuiltInAdmin);
        Assert.Equal(Roles.User, stored.Role);
    }

    [Fact]
    public async Task Login_succeeds_and_token_carries_issuer_and_role()
    {
        await _controller.Register(Reg("admin"));

        var result = await _controller.Login(new LoginRequest { Username = "admin", Password = "Password1!" });

        var response = result.Value;
        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response!.Token));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Equal("test-issuer", token.Issuer);
        // Assert by value to stay independent of inbound/outbound claim-type mapping.
        Assert.Contains(token.Claims, c => c.Value == Roles.Admin);
        Assert.Contains(token.Claims, c => c.Value == "admin");
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        await _controller.Register(Reg("admin"));

        var result = await _controller.Login(new LoginRequest { Username = "admin", Password = "wrong" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_with_unknown_user_is_unauthorized()
    {
        var result = await _controller.Login(new LoginRequest { Username = "ghost", Password = "Password1!" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Restricted_account_cannot_log_in()
    {
        await _controller.Register(Reg("admin"));   // first -> admin
        await _controller.Register(Reg("user"));    // second -> normal user
        var user = await _users.FindByNameAsync("user");
        await _users.SetLoginRestrictedAsync(user!, true);

        var result = await _controller.Login(new LoginRequest { Username = "user", Password = "Password1!" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
