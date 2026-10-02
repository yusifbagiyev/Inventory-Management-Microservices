namespace NotificationService.Application.Services
{
    /// <summary>In-memory map of user ids to their open SignalR connections.</summary>
    public class ConnectionManager : IConnectionManager
    {
        private readonly Dictionary<string, HashSet<string>> _connections = new();
        private readonly object _lock = new();

        public Task AddConnection(string userId, string connectionId)
        {
            lock (_lock)
            {
                if (!_connections.ContainsKey(userId))
                {
                    _connections[userId] = new HashSet<string>();
                }
                _connections[userId].Add(connectionId);
            }
            return Task.CompletedTask;
        }

        public Task RemoveConnection(string userId, string connectionId)
        {
            lock (_lock)
            {
                if (_connections.ContainsKey(userId))
                {
                    _connections[userId].Remove(connectionId);
                    if (_connections[userId].Count == 0)
                    {
                        _connections.Remove(userId);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetConnections(string userId)
        {
            lock (_lock)
            {
                return Task.FromResult<IEnumerable<string>>(
                    _connections.ContainsKey(userId) ? _connections[userId] : Enumerable.Empty<string>()
                );
            }
        }
    }
}