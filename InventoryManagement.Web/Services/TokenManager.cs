using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenManager> _logger;
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
        private const int REFRESH_COOLDOWN_SECONDS = 5;
        private const int MAX_SESSION_DAYS = 30;

        public TokenManager(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenManager> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<string?> GetValidTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogWarning("No HTTP context available");
                return null;
            }

            if (!(context.User?.Identity?.IsAuthenticated ?? false))
            {
                _logger.LogDebug("User not authenticated, cannot provide token");
                return null;
            }

            if (await HasSessionExpiredAsync())
            {
                _logger.LogInformation("User session exceeded {Days} days, forcing logout", MAX_SESSION_DAYS);
                await ClearAllTokensAsync();
                return null;
            }

            var token = context.Session.GetString("JwtToken");

            // The session can be lost while the auth cookie lives on, so restore the token from the refresh cookie.
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("No JWT token in session for authenticated user {User}, attempting restore",
                    context.User.Identity.Name);

                var restored = await RefreshTokenAsync();
                if (restored)
                {
                    token = context.Session.GetString("JwtToken");
                    _logger.LogInformation("Successfully restored JWT token from refresh token");
                }
                else
                {
                    _logger.LogWarning("Failed to restore JWT token - refresh token may be invalid");
                    return null;
                }
            }

            if (!string.IsNullOrEmpty(token) && IsTokenExpiredOrExpiring(token))
            {
                _logger.LogInformation("JWT token expiring soon, refreshing...");
                var refreshed = await RefreshTokenAsync();

                if (refreshed)
                {
                    token = context.Session.GetString("JwtToken");
                    _logger.LogInformation("JWT token refreshed successfully");
                }
                else
                {
                    _logger.LogWarning("Token refresh failed");
                    await ClearAllTokensAsync();
                    return null;
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));
            }

            return token;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            // The cooldown and the lock are static, so they apply across all users.
            var timeSinceLastRefresh = DateTime.Now - _lastRefreshAttempt;
            if (timeSinceLastRefresh.TotalSeconds < REFRESH_COOLDOWN_SECONDS)
            {
                _logger.LogDebug("Refresh attempted too soon, skipping (cooldown: {Seconds}s)",
                    REFRESH_COOLDOWN_SECONDS);
                return false;
            }

            if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("Could not acquire refresh lock within timeout");
                return false;
            }

            try
            {
                _lastRefreshAttempt = DateTime.Now;

                var context = _httpContextAccessor.HttpContext;
                if (context == null) return false;

                var refreshToken = context.Request.Cookies["refresh_token"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("No refresh token available in cookie");
                    return false;
                }

                // Empty when the session was lost.
                var currentAccessToken = context.Session.GetString("JwtToken") ?? string.Empty;

                _logger.LogInformation("Calling auth service to refresh JWT token...");

                var newTokens = await _authService.RefreshTokenAsync(refreshToken, currentAccessToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh failed - received invalid response from auth service");
                    return false;
                }

                context.Session.SetString("JwtToken", newTokens.AccessToken);
                context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                if (newTokens.User != null)
                {
                    context.Session.SetString("UserData", JsonConvert.SerializeObject(new
                    {
                        newTokens.User.Id,
                        newTokens.User.Username,
                        newTokens.User.Email,
                        newTokens.User.FirstName,
                        newTokens.User.LastName
                    }));
                }

                // Every refresh issues a new refresh token, so the cookie must be rewritten.
                var rememberMe = context.Request.Cookies["remember_me"] == "true";
                var refreshCookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = rememberMe
                        ? DateTimeOffset.Now.AddDays(30)
                        : DateTimeOffset.Now.AddHours(1),
                    Path = "/",
                    IsEssential = true
                };
                context.Response.Cookies.Append("refresh_token", newTokens.RefreshToken, refreshCookieOptions);

                _logger.LogInformation("JWT token refreshed and stored successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return false;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private bool IsTokenExpiredOrExpiring(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var expiryTime = jwtToken.ValidTo.ToLocalTime();
                var now = DateTime.Now;

                // Refresh early, once less than 5 minutes are left.
                var bufferTime = TimeSpan.FromMinutes(5);
                var expiresIn = expiryTime - now;

                if (expiresIn <= bufferTime)
                {
                    _logger.LogInformation("Token expires at {ExpiryTime}, current time {Now}, expires in {ExpiresIn}",
                        expiryTime, now, expiresIn);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JWT token");
                return true; // An unreadable token counts as expired
            }
        }

        private async Task<bool> HasSessionExpiredAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            // Only remember-me sessions can last long enough to hit the limit.
            var rememberMe = context.Request.Cookies["remember_me"] == "true";
            if (!rememberMe) return false;

            var loginTimeClaim = context.User.FindFirst("LoginTime");
            if (loginTimeClaim == null) return false;

            if (DateTime.TryParse(loginTimeClaim.Value, out DateTime loginTime))
            {
                var sessionAge = DateTime.Now - loginTime;
                if (sessionAge.TotalDays >= MAX_SESSION_DAYS)
                {
                    return true;
                }
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task ClearAllTokensAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            _logger.LogInformation("Clearing all stored tokens for user {User}",
                context.User?.Identity?.Name ?? "unknown");

            context.Session.Remove("JwtToken");
            context.Session.Remove("UserData");
            context.Session.Remove("LastActivity");

            // Delete must use the same options the cookie was written with.
            context.Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            await Task.CompletedTask;
        }
    }
}