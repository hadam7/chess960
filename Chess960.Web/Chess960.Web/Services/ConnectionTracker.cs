using System.Collections.Concurrent;

namespace Chess960.Web.Services;

public interface IConnectionTracker
{
    void UserConnected(string userId);
    void UserDisconnected(string userId);
    bool IsUserOnline(string userId);
    IEnumerable<string> GetOnlineUsers();
}

public class ConnectionTracker : IConnectionTracker
{
    // Map UserId -> Count of connections (user might have multiple tabs)
    private readonly ConcurrentDictionary<string, int> _onlineUsers = new();

    public void UserConnected(string userId)
    {
        _onlineUsers.AddOrUpdate(userId, 1, (key, count) => count + 1);
    }

    public void UserDisconnected(string userId)
    {
        _onlineUsers.AddOrUpdate(userId, 0, (key, count) => Math.Max(0, count - 1));
        
        // Optional: Remove if 0 to keep dictionary clean?
        // For now, checks are cheap even if 0.
        if (_onlineUsers.TryGetValue(userId, out int count) && count <= 0)
        {
            _onlineUsers.TryRemove(userId, out _);
        }
    }

    public bool IsUserOnline(string userId)
    {
        return _onlineUsers.TryGetValue(userId, out int count) && count > 0;
    }

    public IEnumerable<string> GetOnlineUsers()
    {
        return _onlineUsers.Where(x => x.Value > 0).Select(x => x.Key);
    }
}
