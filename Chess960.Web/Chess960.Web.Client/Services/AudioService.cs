using Microsoft.JSInterop;

namespace Chess960.Web.Client.Services;

public class AudioService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public AudioService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/audio.js");
        await _module.InvokeVoidAsync("initAudio");
    }

    public async Task PlayMove()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "move");
    }

    public async Task PlayCapture()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "capture");
    }

    public async Task PlayGameStart()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "game-start");
    }

    public async Task PlayGameEnd()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "game-end");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
