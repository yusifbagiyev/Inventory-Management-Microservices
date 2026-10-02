namespace ApiGateway.Middleware
{
    public class RequestTimeoutMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestTimeoutMiddleware> _logger;
        private readonly TimeSpan _timeout;

        public RequestTimeoutMiddleware(
            RequestDelegate next,
            ILogger<RequestTimeoutMiddleware> logger,
            TimeSpan? timeout=null)
        {
            _next= next;
            _logger= logger;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var cts=new CancellationTokenSource(_timeout);
            var originalCancellationToken = context.RequestAborted;

            // Cancel on whichever comes first, the timeout or the client going away.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cts.Token, originalCancellationToken);

            context.RequestAborted = linkedCts.Token;

            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Request {Method} {Path} exceeded timeout of {Timeout}ms",
                    context.Request.Method,
                    context.Request.Path,
                    _timeout.TotalMilliseconds);

                context.Response.StatusCode = 504;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "The request took too long to complete",
                    timeout = _timeout.TotalSeconds
                });
            }
            finally
            {
                context.RequestAborted = originalCancellationToken;
            }
        }
    }
}