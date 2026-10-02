using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class UrlService : IUrlService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public UrlService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        /// <summary>Makes an API image path absolute, pointing at the gateway in development.</summary>
        public string? GetImageUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            var baseUrl = _environment.IsDevelopment()
                ? _configuration[$"ApiGateway:BaseUrl"]
                : "https://inventory166.az";

            return $"{baseUrl}{relativePath}";
        }
    }
}