using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
// Trigger Rebuild
using Microsoft.AspNetCore.Components.Authorization;
using Chess960.Web.Client.Services;
using Chess960.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddScoped<ChessGameService>();
builder.Services.AddScoped<MultiplayerService>();
builder.Services.AddScoped<PieceThemeService>();
builder.Services.AddScoped<ClientFriendService>();
builder.Services.AddScoped<AudioService>();

var host = builder.Build();

// Warm up the Chess Engine immediately!
// This ensures that the heavy static initialization happens while the user is on the home/login page.
// We don't await it here to not block the app startup, it runs in background.
var gameService = host.Services.GetRequiredService<ChessGameService>();
_ = gameService.InitializeAsync(); 

await host.RunAsync();
