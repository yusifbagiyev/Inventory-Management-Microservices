using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers
{
    /// <summary>Manual checks of the WhatsApp setup, not meant for production use.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class WhatsAppTestController : ControllerBase
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppTestController> _logger;

        public WhatsAppTestController(
            IWhatsAppService whatsAppService,
            IConfiguration configuration,
            ILogger<WhatsAppTestController> logger)
        {
            _whatsAppService = whatsAppService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("test-group")]
        public async Task<IActionResult> TestGroupMessage([FromBody] TestMessageDto dto)
        {
            try
            {
                var groupId = _configuration["WhatsApp:DefaultGroupId"];
                if (string.IsNullOrEmpty(groupId))
                {
                    return BadRequest(new { error = "WhatsApp group ID not configured" });
                }

                var success = await _whatsAppService.SendGroupMessageAsync(groupId, dto.Message);

                if (success)
                {
                    return Ok(new { success = true, message = "WhatsApp message sent successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to send WhatsApp message" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing WhatsApp group message");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("test-product-notification")]
        public async Task<IActionResult> TestProductNotification()
        {
            try
            {
                var groupId = _configuration["WhatsApp:DefaultGroupId"];
                if (string.IsNullOrEmpty(groupId))
                {
                    return BadRequest(new { error = "WhatsApp group ID not configured" });
                }

                var testNotification = new WhatsAppProductNotification
                {
                    ProductId = 999,
                    InventoryCode = 1234,
                    Model = "Test Product Model",
                    Vendor = "Test Vendor",
                    ToDepartmentName = "Test Department",
                    ToWorker = "Test Worker",
                    CreatedAt = DateTime.Now,
                    IsNewItem = true,
                    NotificationType = "created"
                };

                var message = _whatsAppService.FormatNotification(testNotification);
                var success = await _whatsAppService.SendGroupMessageAsync(groupId, message);

                if (success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Test product notification sent successfully",
                        formattedMessage = message
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to send test notification" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing WhatsApp product notification");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("status")]
        public IActionResult GetWhatsAppStatus()
        {
            var enabled = _configuration.GetValue<bool>("WhatsApp:Enabled", false);
            var groupId = _configuration["WhatsApp:DefaultGroupId"];
            var instanceId = _configuration["WhatsApp:IdInstance"];

            return Ok(new
            {
                enabled,
                instanceConfigured = !string.IsNullOrEmpty(instanceId),
                groupConfigured = !string.IsNullOrEmpty(groupId),
                instanceId = string.IsNullOrEmpty(instanceId) ? "Not configured" : $"***{instanceId.Substring(Math.Max(0, instanceId.Length - 4))}"
            });
        }
    }

    public record TestMessageDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
