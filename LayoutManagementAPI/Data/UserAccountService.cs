using LayoutManagementAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Data;

/// <summary>
/// Custom user store + authentication operations, replacing ASP.NET Identity's
/// UserManager/RoleManager. Backed directly by <see cref="RealEstateDbContext"/>.
/// </summary>
public class UserAccountService
{
    private readonly RealEstateDbContext _db;

    public UserAccountService(RealEstateDbContext db) => _db = db;

    public static string Normalize(string userName) => userName.Trim().ToUpperInvariant();

    public IQueryable<AppUser> Query() => _db.Users.AsQueryable();

    public Task<AppUser?> FindByIdAsync(string id) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<AppUser?> FindByNameAsync(string userName) =>
        _db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == Normalize(userName));

    public Task<AppUser?> FindBuiltInAdminAsync() =>
        _db.Users.FirstOrDefaultAsync(u => u.IsBuiltInAdmin);

    public Task<bool> AnyUsersAsync() => _db.Users.AnyAsync();

    public Task<bool> UserNameTakenAsync(string userName, string? excludeId = null)
    {
        var normalized = Normalize(userName);
        return _db.Users.AnyAsync(u => u.NormalizedUserName == normalized && (excludeId == null || u.Id != excludeId));
    }

    public bool VerifyPassword(AppUser user, string password) =>
        PasswordHasher.Verify(password, user.PasswordHash);

    /// <summary>Creates a user with a hashed password. Caller must check the username is free first.</summary>
    public async Task<AppUser> CreateAsync(
        string userName, string email, string fullName, string password, string role,
        bool isBuiltInAdmin = false)
    {
        var user = new AppUser
        {
            UserName = userName.Trim(),
            NormalizedUserName = Normalize(userName),
            Email = email.Trim(),
            FullName = fullName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            IsBuiltInAdmin = isBuiltInAdmin
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public Task SetPasswordAsync(AppUser user, string password)
    {
        user.PasswordHash = PasswordHasher.Hash(password);
        return _db.SaveChangesAsync();
    }

    public Task SetUserNameAsync(AppUser user, string userName)
    {
        user.UserName = userName.Trim();
        user.NormalizedUserName = Normalize(userName);
        return _db.SaveChangesAsync();
    }

    public Task SetRoleAsync(AppUser user, string role)
    {
        user.Role = role;
        return _db.SaveChangesAsync();
    }

    public Task SetLoginRestrictedAsync(AppUser user, bool restricted)
    {
        user.LoginRestricted = restricted;
        return _db.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
