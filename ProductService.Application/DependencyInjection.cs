using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Behaviors;
using ProductService.Application.Interfaces;
using ProductService.Application.Services;
using System.Reflection;

namespace ProductService.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            services.AddAutoMapper(assembly);
            services.AddValidatorsFromAssembly(assembly);
            services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));

            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IProductManagementService, ProductManagementService>();


            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            return services;
        }
    }
}
