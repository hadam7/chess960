using System.Net.Http.Json;

namespace Chess960.Web.Client.Services;

public class ClientFriendService
{
    private readonly HttpClient _http;

    public ClientFriendService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FriendDto>> GetFriendsAsync()
    {
        try 
        {
            return await _http.GetFromJsonAsync<List<FriendDto>>("api/friends") ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientFriendService] GetFriendsAsync Error: {ex.Message}");
            return new();
        }
    }

    public async Task<List<FriendRequestDto>> GetPendingRequestsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<FriendRequestDto>>("api/friends/requests") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<string?> SendFriendRequestAsync(string targetUsername)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/friends/request", targetUsername);
            if (response.IsSuccessStatusCode)
            {
                return null; // Success
            }
            else
            {
                return await response.Content.ReadAsStringAsync(); // Return error message
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientFriendService] ERROR: {ex.Message}");
            return $"Hálózati hiba: {ex.Message}";
        }
    }

    public async Task AcceptFriendRequestAsync(int friendshipId)
    {
        await _http.PostAsync($"api/friends/accept/{friendshipId}", null);
    }

    public async Task DeclineFriendRequestAsync(int friendshipId)
    {
        await _http.PostAsync($"api/friends/decline/{friendshipId}", null);
    }

    public async Task RemoveFriendAsync(int friendshipId)
    {
        await _http.DeleteAsync($"api/friends/{friendshipId}");
    }
}

public class FriendDto
{
    public int FriendshipId { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Status { get; set; } = "Offline";
}

public class FriendRequestDto
{
    public int FriendshipId { get; set; }
    public string RequesterId { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public DateTime SentAt { get; set; }
}
