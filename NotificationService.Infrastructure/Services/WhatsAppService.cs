using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>Sends messages and photos to a WhatsApp group through Green API.</summary>
    public class WhatsAppService:IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly WhatsAppSettings _settings;

        public WhatsAppService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _settings = configuration.GetSection("Whatsapp").Get<WhatsAppSettings>()
                ?? throw new InvalidOperationException("Whatsapp settings not found in configuration");

            _httpClient.BaseAddress = new Uri(_settings.ApiUrl);
        }

        public async Task<bool> SendGroupMessageAsync(string groupId,string message)
        {
            try
            {
                // Green API expects group chat ids to end in @g.us
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                var payload = new
                {
                    chatId = groupId,
                    message
                };

                var endpoint = $"/waInstance{_settings.IdInstance}/sendMessage/{_settings.ApiTokenInstance}";

                var response = await SendRequestAsync(endpoint, payload);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Whatsapp message sent succesfully to group {groupId}");
                    return true;
                }
                var errorContent=await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to send WhatsApp message. Status: {response.StatusCode}, Error: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending WhatsApp message to group {groupId}");
                return false;
            }
        }

        public async Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName)
        {
            try
            {
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                if (message.Length > 2048)
                {
                    _logger.LogWarning("Caption exceeds 2048 characters. Truncating...");
                    message = message.Substring(0, 2045) + "...";
                }

                var imageSizeInMB = imageData.Length / (1024.0 * 1024.0);
                _logger.LogInformation($"Image size: {imageSizeInMB:F2} MB for file: {fileName}");

                // Green API refuses files over about 10 MB, so send the text alone
                if (imageSizeInMB > 10)
                {
                    _logger.LogWarning($"Image size {imageSizeInMB:F2}MB exceeds limit. Sending text only.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                var mimeType = GetMimeType(fileName);
                var endpoint = $"/waInstance{_settings.IdInstance}/sendFileByUpload/{_settings.ApiTokenInstance}";

                using var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(groupId), "chatId");
                formData.Add(new StringContent(message), "caption");

                var fileContent = new ByteArrayContent(imageData);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

                var sanitizedFileName = SanitizeFileName(fileName ?? $"image.{mimeType.Split('/')[1]}");
                formData.Add(fileContent, "file", sanitizedFileName);

                _logger.LogDebug($"Sending file to: {_httpClient.BaseAddress}{endpoint}");
                _logger.LogDebug($"File size: {imageData.Length} bytes, MIME: {mimeType}, Name: {sanitizedFileName}");

                var response = await _httpClient.PostAsync(endpoint, formData);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"WhatsApp image sent to group {groupId}");
                    return true;
                }

                _logger.LogError($"Failed to upload file. Status: {response.StatusCode}, Error: {responseContent}");
                return await SendGroupMessageAsync(groupId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending image to group {groupId}");
                return await SendGroupMessageAsync(groupId, message);
            }
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _=> "image/jpeg"
            };
        }
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleanName = new string(fileName
                .Where(ch => !invalidChars.Contains(ch))
                .ToArray());

            return Path.GetExtension(cleanName) == ""
                ? $"{cleanName}.jpg"
                : cleanName;
        }

        public string FormatNotification(WhatsAppProductNotification notification)
        {
            var message = new StringBuilder();

            var emoji = notification.NotificationType switch
            {
                "created" => "✅",
                "transferred" => "🔄",
                _ => "📌"
            };

            message.AppendLine($"{emoji} *Product {notification.NotificationType.ToUpper()}*");
            message.AppendLine();

            message.AppendLine($"📦 *Product Details:*");
            message.AppendLine($"• *Inventory Code:* {notification.InventoryCode}");
            message.AppendLine($"• *Category:* {notification.CategoryName}");
            message.AppendLine($"• *Vendor:* {notification.Vendor}");
            message.AppendLine($"• *Model:* {notification.Model}");
            if (notification.NotificationType == "created")
            {
                message.AppendLine($"• *Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                {
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
                }

                if (notification.IsNewItem)
                {
                    message.AppendLine($"• *Status:* 🆕 New Item");
                }

                if(!notification.IsWorking)
                {
                    message.AppendLine($"• *Status:* ❌ Not Working");
                }
            }
            else if(notification.NotificationType =="transferred")
            {
                message.AppendLine($"• *From Department:* {notification.FromDepartmentName}");

                if (!string.IsNullOrEmpty(notification.FromWorker))
                {
                    message.AppendLine($"• *From Worker:* {notification.FromWorker}");
                }

                message.AppendLine($"• *To Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                {
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
                }
            }

            if (!string.IsNullOrEmpty(notification.Notes))
            {
                message.AppendLine($"• *Notes:* {notification.Notes}");
            }

            message.AppendLine();
            message.AppendLine($"⏰ *Time:* {notification.CreatedAt:dd/MM/yyyy HH:mm}");
            return message.ToString();
        }


        private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug($"Sending request to: {_httpClient.BaseAddress}{endpoint}");

            var payloadPreview = json.Length > 100 ? json.Substring(0, 100) + "..." : json;
            _logger.LogDebug($"Payload preview: {payloadPreview}");

            return await _httpClient.PostAsync(endpoint, content);
        }
    }
}