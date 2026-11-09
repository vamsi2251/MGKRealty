using LayoutManagementAPI.Data;
using Xunit;

namespace LayoutManagement.Tests;

public class UserAccountServiceTests : IDisposable
{
    private readonly TestDatabase _test = new();
    private readonly UserAccountService _users;

    public UserAccountServiceTests() => _users = new UserAccountService(_test.Db);

    public void Dispose() => _test.Dispose();

    [Fact]
    public async Task Create_stores_hashed_password_and_normalized_name()
    {
        var user = await _users.CreateAsync("Alice", "alice@example.com", "Alice A", "Password1!", Roles.User);

        Assert.Equal("ALICE", user.NormalizedUserName);
        Assert.NotEqual("Password1!", user.PasswordHash);
        Assert.True(_users.VerifyPassword(user, "Password1!"));
        Assert.False(_users.VerifyPassword(user, "nope"));
    }

    [Fact]
    public async Task FindByName_is_case_insensitive()
    {
        await _users.CreateAsync("Bob", "bob@example.com", "Bob", "Password1!", Roles.User);

        Assert.NotNull(await _users.FindByNameAsync("bob"));
        Assert.NotNull(await _users.FindByNameAsync("BOB"));
        Assert.Null(await _users.FindByNameAsync("charlie"));
    }

    [Fact]
    public async Task UserNameTaken_respects_exclude_id()
    {
        var user = await _users.CreateAsync("Dana", "dana@example.com", "Dana", "Password1!", Roles.User);

        Assert.True(await _users.UserNameTakenAsync("dana"));
        Assert.False(await _users.UserNameTakenAsync("dana", excludeId: user.Id));
        Assert.False(await _users.UserNameTakenAsync("eve"));
    }

    [Fact]
    public async Task SetPassword_replaces_old_hash()
    {
        var user = await _users.CreateAsync("Finn", "finn@example.com", "Finn", "OldPass1!", Roles.User);

        await _users.SetPasswordAsync(user, "NewPass1!");

        Assert.False(_users.VerifyPassword(user, "OldPass1!"));
        Assert.True(_users.VerifyPassword(user, "NewPass1!"));
    }

    [Fact]
    public async Task SetUserName_updates_normalized_name()
    {
        var user = await _users.CreateAsync("Gail", "gail@example.com", "Gail", "Password1!", Roles.User);

        await _users.SetUserNameAsync(user, "Gabriella");

        Assert.Equal("GABRIELLA", user.NormalizedUserName);
        Assert.NotNull(await _users.FindByNameAsync("gabriella"));
        Assert.Null(await _users.FindByNameAsync("gail"));
    }

    [Fact]
    public async Task SetRole_and_login_restriction_persist_across_contexts()
    {
        var user = await _users.CreateAsync("Hank", "hank@example.com", "Hank", "Password1!", Roles.User);

        await _users.SetRoleAsync(user, Roles.Admin);
        await _users.SetLoginRestrictedAsync(user, true);

        using var fresh = _test.NewContext();
        var reread = await new UserAccountService(fresh).FindByIdAsync(user.Id);

        Assert.Equal(Roles.Admin, reread!.Role);
        Assert.True(reread.LoginRestricted);
    }

    [Fact]
    public async Task AnyUsers_and_builtin_admin_lookup()
    {
        Assert.False(await _users.AnyUsersAsync());

        var admin = await _users.CreateAsync("root", "root@example.com", "Root", "Password1!", Roles.Admin, isBuiltInAdmin: true);

        Assert.True(await _users.AnyUsersAsync());
        var found = await _users.FindBuiltInAdminAsync();
        Assert.Equal(admin.Id, found!.Id);
    }

    [Fact]
    public async Task Duplicate_username_is_rejected_by_unique_index()
    {
        await _users.CreateAsync("same", "a@example.com", "A", "Password1!", Roles.User);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _users.CreateAsync("SAME", "b@example.com", "B", "Password1!", Roles.User));
    }
}
