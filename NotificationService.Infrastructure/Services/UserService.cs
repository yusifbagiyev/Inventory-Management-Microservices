using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>Reads users from IdentityService, signing its calls with a short-lived system token.</summary>
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        public UserService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<UserService> logger)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration["Services:IdentityService"] ?? "http://localhost:5003");
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<UserDto>> GetUsersAsync(string? role = null)
        {
            try
            {
                SetSystemAuthorizationHeader();

                var url = string.IsNullOrEmpty(role)
                    ? "/api/auth/users"
                    : $"/api/auth/users/by-role/{Uri.EscapeDataString(role)}";

                _logger.LogInformation($"Fetching users from: {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<UserDto>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _logger.LogInformation($"Found {users?.Count ?? 0} users");
                    return users ?? new List<UserDto>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to get users: {response.StatusCode} - {errorContent}");
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users for role: {role}");
                return new List<UserDto>();
            }
        }

        public async Task<UserDto?> GetUserAsync(int userId)
        {
            SetSystemAuthorizationHeader();

            var response = await _httpClient.GetAsync($"/api/auth/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserDto>();
            }
            return null;
        }

        public async Task<List<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            try
            {
                SetSystemAuthorizationHeader();

                var url = $"/api/auth/users/by-role/{role}";
                _logger.LogInformation($"Getting users for role {role} from: {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.GetAsync(url, cancellationToken);

                _logger.LogInformation($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken: cancellationToken);
                    var userIds = users?.Select(u => u.Id).ToList() ?? new List<int>();
                    _logger.LogInformation($"Found {userIds.Count} users with role {role}");
                    return userIds;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError($"Failed to get users for role {role}: {response.StatusCode} - {errorContent}");
                return new List<int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users for role {role}");
                return new List<int>();
            }
        }

        private void SetSystemAuthorizationHeader()
        {
            var token = GenerateSystemToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("Set authorization header with system token");
        }

        private string GenerateSystemToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, "0"),
                    new Claim(ClaimTypes.Name, "System"),
                    new Claim(ClaimTypes.Role, "Admin"), // The user endpoints in IdentityService are admin-only
                    new Claim(ClaimTypes.Role, "System")
                }),
                Expires = DateTime.Now.AddMinutes(5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}