using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Chess960.Web.Client.Services;

// This service deserializes the authentication state from the server (prerendered HTML)
// so that the client (WASM) has immediate access to user claims without waiting for an API call.
public class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> _defaultUnauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> _authenticationStateTask = _defaultUnauthenticatedTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState state)
    {
        if (state.TryTakeFromJson<UserInfo>("UserInfo", out var userInfo) && userInfo is not null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userInfo.UserId),
                new Claim(ClaimTypes.Name, userInfo.Name),
                new Claim(ClaimTypes.Email, userInfo.Email)
            };

            var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(claims, nameof(PersistentAuthenticationStateProvider)));
            _authenticationStateTask = Task.FromResult(new AuthenticationState(authenticatedUser));
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
