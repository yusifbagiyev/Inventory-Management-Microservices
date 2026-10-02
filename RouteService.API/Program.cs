using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RouteService.Application;
using RouteService.Application.Mappings;
using RouteService.Infrastructure;
using RouteService.Infrastructure.Data;
using Serilog;
using Serilog.Events;
using SharedServices.Authorization;
using SharedServices.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Route Service")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting RouteService API");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Route Service API",
        Version = "v1",
        Description = "Route Service with JWT Authentication"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n " +
                      "Enter your token in the text input below.\r\n\r\n" +
                      "Example: \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AllPermissions.RouteView, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteView)));
    options.AddPolicy(AllPermissions.RouteCreate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteCreate)));
    options.AddPolicy(AllPermissions.RouteUpdate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteUpdate)));
    options.AddPolicy(AllPermissions.RouteDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteDelete)));
    options.AddPolicy(AllPermissions.RouteComplete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteComplete)));
    options.AddPolicy(AllPermissions.RouteCreateDirect, policy => 
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteCreateDirect)));
    options.AddPolicy(AllPermissions.RouteUpdateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteUpdateDirect)));
    options.AddPolicy(AllPermissions.RouteDeleteDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteDeleteDirect)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
    dbContext.Database.Migrate();
    dbContext.Database.EnsureCreated();
}

app.Run();