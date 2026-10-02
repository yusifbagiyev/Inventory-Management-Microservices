using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using InventoryManagement.Web.Services;
using Serilog;
using Serilog.Events;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();

    Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Inventory Web")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting InventoryManagement.Web application");

    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    builder.Services.AddCustomAuthentication(builder.Configuration);

    builder.Services.AddDistributedMemoryCache(); // Session storage
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromDays(7);
        options.Cookie.Name = ".InventoryManagement.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.Cookie.MaxAge= TimeSpan.FromDays(7);
        options.IOTimeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHostedService<TokenRefreshBackgroundService>();


    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = 401;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = 403;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Production", policy =>
        {
            policy.WithOrigins(
                    "https://inventory.local",
                    "https://www.inventory.local",
                    "https://api.inventory.local")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });

        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5051",
                    "https://localhost:7171")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });


    builder.Services.AddHttpClient<ApiService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });


    builder.Services.AddHttpContextAccessor();

    builder.Services.AddCustomServices();

    builder.Services.AddSignalR();

    builder.Services.AddMemoryCache();


    var app = builder.Build();

    if (app.Environment.IsProduction())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseCors("Production");
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseCors("Development");
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseStatusCodePagesWithReExecute("/NotFound", "?statusCode={0}");

    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();

    app.UseAuthentication();
      
    app.UseMiddleware<JwtMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("InventoryManagement.Web application configured successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}