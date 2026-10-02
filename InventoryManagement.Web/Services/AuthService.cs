using System.Text;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(HttpClient httpClient, 
            IConfiguration configuration, 
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
        }

        public async Task<TokenDto?> LoginAsync(string username, string password, bool rememberMe = false)
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                var clientIp = context?.Connection.RemoteIpAddress?.ToString();
                var forwardedFor = context?.Request.Headers["X-Forwarded-For"].FirstOrDefault();

                var loginDto = new LoginDto
                {
                    Username = username,
                    Password = password
                };

                var json = JsonConvert.SerializeObject(loginDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Headers go on this message, not on the shared client.
                var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
                {
                    Content = content
                };

                // Pass the real client IP on to the identity service.
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    request.Headers.Add("X-Forwarded-For", forwardedFor);
                    var realIp = forwardedFor.Split(',')[0].Trim();
                    request.Headers.Add("X-Real-IP", realIp);
                    _logger.LogDebug("Login request - forwarding X-Forwarded-For: {ForwardedFor}", forwardedFor);
                }
                else if (!string.IsNullOrEmpty(clientIp))
                {
                    request.Headers.Add("X-Forwarded-For", clientIp);
                    request.Headers.Add("X-Real-IP", clientIp);
                    _logger.LogDebug("Login request - starting X-Forwarded-For with: {ClientIp}", clientIp);
                }

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TokenDto>(responseContent);

                    if (result != null)
                    {
                        // Remember me comes from the login form, not from the API.
                        result.RememberMe = rememberMe;
                        _logger.LogInformation("Login successful for user: {Username}", username);
                    }

                    return result;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Login failed with status code: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Username}", username);
                return null;
            }
        }

        public async Task<TokenDto?> RefreshTokenAsync(string refreshToken,string accessToken)
        {
            try
            {
                var refreshDto = new RefreshTokenDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };

                var json = JsonConvert.SerializeObject(refreshDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // The refresh call goes without the expired bearer token.
                _httpClient.DefaultRequestHeaders.Authorization = null;

                var response = await _httpClient.PostAsync("api/auth/refresh", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TokenDto>(responseContent);

                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        _logger.LogInformation("Token refresh successful");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Token refresh returned invalid data");
                        return null;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Token refresh failed with status {Status}: {Content}",
                    response.StatusCode, errorContent);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return null;
            }
        }
    }
}