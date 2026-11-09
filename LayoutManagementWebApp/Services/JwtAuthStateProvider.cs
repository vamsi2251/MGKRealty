using LayoutManagementWebApp.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LayoutManagementWebApp.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private const string Key = "realestate-auth";
    private readonly ProtectedSessionStorage _storage;
    private readonly AuthSession _session;

    public JwtAuthStateProvider(ProtectedSessionStorage storage, AuthSession session)
    {
        _storage = storage;
        _session = session;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_session.Principal()));

    public async Task RestoreAsync()
    {
        try
        {
            var stored = await _storage.GetAsync<StoredAuth>(Key);
            if (stored.Success && stored.Value is not null && stored.Value.ExpiresOn > DateTime.UtcNow)
            {
                _session.Apply(stored.Value);
            }
            else
            {
                _session.Clear();
                await _storage.DeleteAsync(Key);
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignInAsync(AuthResponse response)
    {
        _session.Apply(response);
        await _storage.SetAsync(Key, new StoredAuth
        {
            Token = response.Token,
            Username = response.Username,
            FullName = response.FullName,
            Email = response.Email,
            Roles = response.Roles,
            ExpiresOn = response.ExpiresOn
        });
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        _session.Clear();
        await _storage.DeleteAsync(Key);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Clears the in-memory session and flips auth state to signed-out without JS interop,
    /// so it is safe to call from inside an API call (e.g. after the API returns 401).
    /// </summary>
    public async Task MarkSessionExpiredAsync()
    {
        _session.Clear();
        try
        {
            await _storage.DeleteAsync(Key);
        }
        catch (InvalidOperationException)
        {
            // JS interop may be unavailable (e.g. during prerender); the stored token
            // is also removed by RestoreAsync on the next load.
        }
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
