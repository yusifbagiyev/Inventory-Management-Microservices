using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ProductService.Application.DTOs;
using ProductService.Application.Interfaces;
using SharedServices.DTOs;

namespace ProductService.Infrastructure.Services
{
    /// <summary>Files approval requests with ApprovalService, passing on the caller's bearer token.</summary>
    public class ApprovalServiceClient : IApprovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApprovalServiceClient(
            HttpClient httpClient, 
            IHttpContextAccessor httpContextAccessor,
            IMediator mediator,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;

            var baseUrl = configuration["Services:ApprovalService"] ?? "http://localhost:5000";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }

            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/approvalrequests", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApprovalRequestDto>(responseContent);
            return result ?? throw new InvalidOperationException("Failed to create approval request");
        }
    }
}