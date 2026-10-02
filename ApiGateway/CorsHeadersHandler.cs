
namespace ApiGateway
{
    /// <summary>Adds CORS headers to downstream responses that come back without them.</summary>
    public class CorsHeadersHandler:DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            var response=await base.SendAsync(request, cancellationToken);

            if(!response.Headers.Contains("Access-Control-Allow-Origin"))
            {
                response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:5051");
                response.Headers.Add("Access-Control-Allow-Credentials", "true");
            }

            if (request.Method == HttpMethod.Options)
            {
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            }
            return response;
        }
    }
}