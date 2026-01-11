using Microsoft.JSInterop;

namespace Chess960.Web.Client.Services;

public class PieceThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private string _currentTheme = "standard";

    public event Action? OnThemeChanged;

    public string CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public PieceThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var theme = await _jsRuntime.InvokeAsync<string>("themeService.getPieceTheme");
            if (!string.IsNullOrEmpty(theme))
            {
                CurrentTheme = theme;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing piece theme: {ex.Message}");
        }
    }

    public async Task SetThemeAsync(string themeName)
    {
        CurrentTheme = themeName;
        await _jsRuntime.InvokeVoidAsync("themeService.setPieceTheme", themeName);
    }
}
