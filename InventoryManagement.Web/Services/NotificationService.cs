using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace InventoryManagement.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public NotificationService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;

            var apiGatewayUrl = configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000";
            _httpClient.BaseAddress = new Uri(apiGatewayUrl);
        }

        private void AddAuthorizationHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false)
        {
            AddAuthorizationHeader();

            try
            {
                var response = await _httpClient.GetAsync($"/api/notifications?unreadOnly={unreadOnly}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<NotificationDto>>()
                        ?? new List<NotificationDto>();
                }
                return new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                // A failed load shows an empty list instead of breaking the page
                Console.WriteLine($"Failed to get notifications: {ex.Message}");
                return new List<NotificationDto>();
            }
        }

        public async Task<int> GetUnreadCountAsync()
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync("/api/notifications/unread-count");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<int>(content);
            }
            return 0;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            AddAuthorizationHeader();

            var dto = new MarkAsReadDto
            {
                NotificationId = notificationId,
            };

            var json = JsonConvert.SerializeObject(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/api/notifications/mark-as-read",content);
            response.EnsureSuccessStatusCode();
        }

        public async Task MarkAllAsReadAsync()
        {
            AddAuthorizationHeader();

            var response = await _httpClient.PostAsync("/api/notifications/mark-all-read", null);
            response.EnsureSuccessStatusCode();
        }
    }
}