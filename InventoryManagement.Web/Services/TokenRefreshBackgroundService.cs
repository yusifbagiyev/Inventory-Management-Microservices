using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class TokenRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenRefreshBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public TokenRefreshBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<TokenRefreshBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();

                        // Without a request there is no signed-in user whose token could be refreshed.
                        if (httpContextAccessor?.HttpContext != null)
                        {
                            var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                            // Refreshes the token when it is close to expiry.
                            await tokenManager.GetValidTokenAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in token refresh background service");
                }
            }
        }
    }
}