using System.Net;
using System.Text.Json;
using FluentValidation;
using ProductService.Application.DTOs;
using SharedServices.Exceptions;

namespace ProductService.API.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = new ErrorResponse();

            switch (exception)
            {
                case ValidationException validationException:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Error="Validation Error";
                    response.ValidationErrors = validationException.Errors
                        .GroupBy(e=> e.PropertyName)
                        .ToDictionary(g=>g.Key,g=>g.Select(e => e.ErrorMessage).ToArray());
                    break;

                case NotFoundException notFoundException:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Error = notFoundException.Message;
                    break;

                case ArgumentException argumentException:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Error = argumentException.Message;
                    break;

                case UnauthorizedAccessException:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Error = "Unauthorized access";
                    break;

                default:
                    context.Response.StatusCode=(int)HttpStatusCode.InternalServerError;
                    response.Error = "An error occurred while processing your request";
                    response.Details = exception.Message;

                    var logger=context.RequestServices.GetService<ILogger<ErrorHandlingMiddleware>>();
                    logger?.LogError(exception, "Unhandled exception occurred");
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}