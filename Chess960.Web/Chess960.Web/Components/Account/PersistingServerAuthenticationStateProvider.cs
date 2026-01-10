using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Chess960.Web.Client;
using Chess960.Web.Data;

namespace Chess960.Web.Components.Account;

// This service persists the authentication state to the client (WASM)
// so that the client doesn't need to re-authenticate immediately or guess claims.
internal sealed class PersistingServerAuthenticationStateProvider : IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingServerAuthenticationStateProvider(
        PersistentComponentState state,
        IOptions<IdentityOptions> options)
    {
        _state = state;
        _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    public PersistingServerAuthenticationStateProvider(PersistentComponentState state)
    {
        _state = state;
        _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        _authenticationStateTask = authenticationStateTask;
    }

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
        }

        var authenticationState = await _authenticationStateTask;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                      ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = principal.Identity.Name; // Or ClaimTypes.Name

            if (userId != null && email != null && name != null)
            {
                _state.PersistAsJson("UserInfo", new UserInfo
                {
                    UserId = userId,
                    Email = email,
                    Name = name,
                });
            }
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
