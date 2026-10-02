namespace NotificationService.Application.DTOs
{
    public record WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string MediaUrl { get; set; } = string.Empty;
        public string IdInstance { get; set; } = string.Empty;
        public string ApiTokenInstance { get; set; } = string.Empty;
        public string DefaultGroupId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
}