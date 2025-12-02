using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Chess960.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddScoped<ChessGameService>();

await builder.Build().RunAsync();
