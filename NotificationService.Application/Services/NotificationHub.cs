using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Services
{
    /// <summary>SignalR hub that puts each connection in its user group and role groups.</summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(IConnectionManager connectionManager, ILogger<NotificationHub> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.AddConnection(userId, Context.ConnectionId);

                // NotificationSender reaches a user only through this group
                var userGroup = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
                _logger.LogInformation($"User {userId} joined group {userGroup}");

                var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
                foreach (var role in roles)
                {
                    var roleGroup = $"role-{role}";
                    await Groups.AddToGroupAsync(Context.ConnectionId, roleGroup);
                    _logger.LogInformation($"User {userId} added to role group: {roleGroup}");
                }

                await Clients.Caller.SendAsync("ConnectionEstablished", new
                {
                    connectionId = Context.ConnectionId,
                    userId = userId,
                    userGroup = userGroup,
                    roleGroups = roles.Select(r => $"role-{r}").ToList(),
                    timestamp = DateTime.Now,
                    message = "Connected successfully"
                });
                _logger.LogInformation($"User {userId} connected with ID {Context.ConnectionId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnection(userId, Context.ConnectionId);
                _logger.LogInformation($"User {userId} disconnected: {exception?.Message ?? "Normal disconnect"}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinUserGroup()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var userGroup = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
                _logger.LogInformation($"User {userId} explicitly joined group {userGroup}");
            }
        }
    }
}