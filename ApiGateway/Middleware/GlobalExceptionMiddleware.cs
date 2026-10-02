using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace ApiGateway.Middleware
{
    /// <summary>Logs slow or failed requests and answers exceptions with a JSON error carrying the correlation id.</summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.TraceIdentifier;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug(
                    "Request started: {Method} {Path} | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);

                await _next(context);

                stopwatch.Stop();

                // Log only slow or failed requests to keep Seq quiet
                if (stopwatch.ElapsedMilliseconds > 1000 || context.Response.StatusCode >= 400)
                {
                    _logger.LogInformation(
                        "Request completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | CorrelationId: {CorrelationId}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        correlationId);
                }
            }
            catch (TaskCanceledException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "Request TIMEOUT: {Method} {Path} | Duration: {Duration}ms | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                await HandleExceptionAsync(context, ex, correlationId, HttpStatusCode.RequestTimeout,
                    "The request took too long to complete. Please try again.");
            }
            catch (OperationCanceledException ex)
            {
                stopwatch.Stop();

                _logger.LogWarning(ex,
                    "Request CANCELLED: {Method} {Path} | Duration: {Duration}ms | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                await HandleExceptionAsync(context, ex, correlationId, HttpStatusCode.BadRequest,
                    "The request was cancelled.");
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();

                // A downstream service is down or unreachable
                _logger.LogError(ex,
                    "Downstream service error: {Method} {Path} | Duration: {Duration}ms | CorrelationId: {CorrelationId} | Message: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId,
                    ex.Message);

                await HandleExceptionAsync(context, ex, correlationId, HttpStatusCode.BadGateway,
                    "Unable to connect to the backend service. Please try again later.");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex,
                    "UNHANDLED EXCEPTION: {Method} {Path} | Duration: {Duration}ms | CorrelationId: {CorrelationId} | ExceptionType: {ExceptionType} | Message: {Message} | StackTrace: {StackTrace}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);

                await HandleExceptionAsync(context, ex, correlationId, HttpStatusCode.InternalServerError,
                    "An unexpected error occurred. Please contact support if the problem persists.");
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception,
            string correlationId,
            HttpStatusCode statusCode,
            string userMessage)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var errorResponse = new
            {
                error = userMessage,
                correlationId = correlationId,
                timestamp = DateTime.UtcNow,
                path = context.Request.Path.ToString()
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}