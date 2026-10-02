using System.Security.Claims;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using NotificationService.Application.Services;

namespace InventoryManagement.Web.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddHttpClient<IApiService, ApiService>();
            services.AddHttpClient<IAuthService, AuthService>();
            services.AddHttpClient<IApprovalService, ApprovalService>();
            services.AddHttpClient<INotificationService, Services.NotificationService>();
            services.AddHttpClient<IUserManagementService, UserManagementService>();

            services.AddScoped<IApiService, ApiService>();
            services.AddScoped<IUrlService, UrlService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IApprovalService, ApprovalService>();
            services.AddScoped<INotificationService, Services.NotificationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<ITokenManager, TokenManager>();
            services.AddScoped<IWordExportService, WordExportService>();

            services.AddSingleton<IConnectionManager, ConnectionManager>();
            return services;
        }

        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<int>("Authentication:CookieExpirationMinutes", 60));
                options.SlidingExpiration = true;
            });

            return services;
        }

        /// <summary>Looks for the permission among the claims copied from the login result at sign-in.</summary>
        public static bool HasPermission(this ClaimsPrincipal user, string permission)
        {
            return user.Claims.Any(c => c.Type == "permission" && c.Value == permission);
        }

        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var words = str.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }
    }
}