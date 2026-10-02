using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.Interfaces;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;
using RouteService.Infrastructure.Repositories;
using RouteService.Infrastructure.Services;

namespace RouteService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<RouteDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(RouteDbContext).Assembly.FullName)));


            services.AddHttpContextAccessor();

            services.AddScoped<IInventoryRouteRepository, InventoryRouteRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddSingleton<RabbitMQConsumer>();
            services.AddHostedService(provider => provider.GetRequiredService<RabbitMQConsumer>());
            services.AddHttpClient<IProductServiceClient, ProductServiceClient>();
            services.AddHttpClient<IApprovalService, ApprovalServiceClient>();
            services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

            return services;
        }
    }
}