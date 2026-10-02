using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ProductService.API.Authentication
{
    /// <summary>Signs in internal services by their X-Api-Key with the permissions configured for that key.</summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Without the header this is not an API key call, so let the other schemes try
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
            {
                return AuthenticateResult.NoResult();
            }

            var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

            if (string.IsNullOrEmpty(providedApiKey))
            {
                return AuthenticateResult.NoResult();
            }

            var validApiKeys = _configuration.GetSection("ApiKeys").Get<Dictionary<string, ApiKeyConfig>>();

            if (validApiKeys == null || !validApiKeys.TryGetValue(providedApiKey, out var apiKeyConfig))
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, apiKeyConfig.ServiceName),
                new Claim(ClaimTypes.NameIdentifier, apiKeyConfig.ServiceId),
                new Claim("ServiceType", "Internal"),
            };

            foreach (var permission in apiKeyConfig.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

    public record ApiKeyConfig
    {
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }
}