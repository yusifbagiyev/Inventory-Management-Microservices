using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(
            RequestDelegate next,
            ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsStaticFile(context) || IsAuthPage(context))
            {
                await _next(context);
                return;
            }

            // Relies on UseAuthentication running before this middleware.
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                using var scope = context.RequestServices.CreateScope();
                var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                try
                {
                    var token = await tokenManager.GetValidTokenAsync();

                    if (!string.IsNullOrEmpty(token))
                    {
                        // ApiService picks the token up from here.
                        context.Items["JwtToken"] = token;

                        context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                        _logger.LogDebug("JWT token available for request to {Path}", context.Request.Path);
                    }
                    else
                    {
                        _logger.LogWarning("Could not obtain valid JWT token for authenticated user {User}",
                            context.User.Identity.Name);

                        // The cookie is still valid, so most likely the refresh token expired.
                        await HandleTokenFailure(context);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error managing JWT token for user {User}",
                        context.User.Identity.Name);

                    await HandleTokenFailure(context);
                    return;
                }
            }
            else
            {
                _logger.LogDebug("User not authenticated for request to {Path}", context.Request.Path);
            }

            await _next(context);
        }

        private async Task HandleTokenFailure(HttpContext context)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();

            context.Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            if (IsAjaxRequest(context.Request))
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Session expired\",\"redirectUrl\":\"/Account/Login\"}");
            }
            else
            {
                context.Response.Redirect("/Account/Login?returnUrl=" +
                    Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            }
        }

        private bool IsStaticFile(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            return path.Contains("/css/") || path.Contains("/js/") ||
                   path.Contains("/images/") || path.Contains("/lib/") ||
                   path.Contains("/sounds/") || path.Contains("favicon.ico") ||
                   path.Contains(".css") || path.Contains(".js") ||
                   path.Contains(".png") || path.Contains(".jpg") ||
                   path.Contains(".jpeg") || path.Contains(".gif") ||
                   path.Contains(".svg") || path.Contains(".woff") ||
                   path.Contains(".woff2") || path.Contains(".ttf");
        }

        private bool IsAuthPage(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            return path.Contains("/account/login") ||
                   path.Contains("/account/logout") ||
                   path.Contains("/account/refreshtoken") ||
                   path == "/" || path == "";
        }

        private bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   request.Headers.Accept.ToString().Contains("application/json");
        }
    }
}