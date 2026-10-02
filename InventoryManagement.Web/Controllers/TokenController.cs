using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/token")]
    public class TokenController : ControllerBase
    {
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<TokenController> _logger;

        public TokenController(ITokenManager tokenManager, ILogger<TokenController> logger)
        {
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>Gives page scripts the JWT for SignalR and direct API calls, refreshing it first if it expired.</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentToken()
        {
            try
            {
                var token = await _tokenManager.GetValidTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { error = "No valid token available" });
                }

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current token");
                return StatusCode(500, new { error = "Failed to retrieve token" });
            }
        }

        /// <summary>Lets health checks see whether the session still has a usable token.</summary>
        [HttpGet("validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var token = await _tokenManager.GetValidTokenAsync();
                var isValid = !string.IsNullOrEmpty(token);

                return Ok(new { isValid, timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return Ok(new { isValid = false, timestamp = DateTime.Now });
            }
        }
    }
}