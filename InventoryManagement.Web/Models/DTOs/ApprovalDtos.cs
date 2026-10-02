namespace InventoryManagement.Web.Models.DTOs
{
    public record ApprovalRequestDto
    {
        public int Id { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string ActionData { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public int? ApprovedById { get; set; }
        public string? ApprovedByName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }


    public record CreateApprovalRequestDto
    {
        public string RequestType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public object ActionData { get; set; } = null!;
    }

    public record ApprovalStatisticsDto
    {
        public int TotalPending { get; set; }
        public int TotalApprovedToday { get; set; }
        public int TotalRejectedToday { get; set; }
    }
}