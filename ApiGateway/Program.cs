using ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Events;
using System.Net;
using System.Text;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();
Log.Information("Starting ApiGateway");

var environmentName = builder.Environment.EnvironmentName;

builder.Services.AddHttpClient("OcelotHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 100,
        // Accepts any certificate. Only safe for development.
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());


var environment=builder.Environment.EnvironmentName;
builder.Configuration.AddJsonFile("ocelot.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"ocelot.{environment}.json", optional: true, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            var allowedOrigins = builder.Environment.IsDevelopment()
            ? new[] { "http://localhost:5051", "https://localhost:7171" }
            : new[] { "https://inventory166.az", "https://inventory166.az" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer",options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddOcelot();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

    // Trust the Docker networks, where nginx sits in front of the gateway.
    KnownNetworks =
    {
        new IPNetwork(IPAddress.Parse("172.18.0.0"), 16),
        new IPNetwork(IPAddress.Parse("172.17.0.0"), 16)
    },

    ForwardLimit = null,
    RequireHeaderSymmetry = false,
    ForwardedForHeaderName = "X-Forwarded-For"
});

app.UseMiddleware<RequestTimeoutMiddleware>(TimeSpan.FromSeconds(30));

app.UseGlobalExceptionHandler();

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();

    Log.Debug("API Gateway received - X-Forwarded-For: {ForwardedFor}, X-Real-IP: {RealIp}, RemoteIP: {RemoteIp}",
        forwardedFor, realIp, remoteIp);

    // Downstream services read the client IP from these headers, so fill them in when nginx did not.
    if (string.IsNullOrEmpty(forwardedFor) && !string.IsNullOrEmpty(remoteIp))
    {
        context.Request.Headers["X-Forwarded-For"] = remoteIp;
    }

    if (string.IsNullOrEmpty(realIp) && !string.IsNullOrEmpty(remoteIp))
    {
        context.Request.Headers["X-Real-IP"] = remoteIp;
    }

    await next();
});

await app.UseOcelot();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx and 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning("Retry {RetryAttempt} after {Delay}ms due to {StatusCode}",
                    retryAttempt, timespan.TotalMilliseconds, outcome.Result?.StatusCode);
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Log.Error("Circuit breaker opened for {Duration}s due to {StatusCode}",
                    duration.TotalSeconds, outcome.Result?.StatusCode);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reset - service recovered");
            });
}

app.Run();