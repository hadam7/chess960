using Microsoft.JSInterop;

namespace Chess960.Web.Client.Services;

public class AudioService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AudioService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_module != null) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_module != null) return;
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/audio.js");
            await _module.InvokeVoidAsync("initAudio");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioService] Init failed: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PlayMove()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "move");
    }

    public async Task PlayCapture()
    {
        if (_module != null) await _module.InvokeVoidAsync("playSound", "capture");
    }

    public async Task PlayCheck()
    {
        // Use 'notify' sound for check
        if (_module != null) await _module.InvokeVoidAsync("playSound", "notify");
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
