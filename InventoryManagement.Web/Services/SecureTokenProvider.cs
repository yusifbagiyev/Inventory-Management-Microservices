using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class SecureTokenProvider : ISecureTokenProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<SecureTokenProvider> _logger;

        public SecureTokenProvider(
            IHttpContextAccessor httpContextAccessor,
            ITokenManager tokenManager,
            ILogger<SecureTokenProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>Only for the SignalR connection, since every other call adds the token server side.</summary>
        public async Task<string?> GetTokenForSignalRAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null || !(context.User?.Identity?.IsAuthenticated ?? false))
            {
                _logger.LogWarning("Attempted to get token for unauthenticated user");
                return null;
            }

            var token = await _tokenManager.GetValidTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No valid token available for user {User}",
                    context.User.Identity.Name);
                return null;
            }

            return token;
        }

        public async Task<bool> ValidateCurrentSessionAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            if (!(context.User?.Identity?.IsAuthenticated ?? false))
            {
                return false;
            }

            // The cookie can outlive the API token, so the session only counts while a token is left
            var token = await _tokenManager.GetValidTokenAsync();
            return !string.IsNullOrEmpty(token);
        }
    }
}