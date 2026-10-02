using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;

namespace RouteService.Infrastructure.Services
{
    /// <summary>Reads products and departments from ProductService with a short-lived system admin token.</summary>
    public class ProductServiceClient : IProductServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ProductServiceClient(HttpClient httpClient, IConfiguration configuration,IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["ProductService:BaseUrl"] ?? "http://localhost:5001";
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        private void AddAuthorizationHeader()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(ClaimTypes.NameIdentifier, "0"),
                    new Claim(ClaimTypes.Name, "System")
                ]),
                Expires = DateTime.Now.AddMinutes(5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenHandler.WriteToken(token));
        }
        public async Task<ProductInfoDto?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/products/{productId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ProductInfoDto>(cancellationToken: cancellationToken);
        }
        public async Task<DepartmentDto?> GetDepartmentByIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/departments/{departmentId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DepartmentDto>(cancellationToken: cancellationToken);
        }
    }
}