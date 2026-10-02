using ApprovalService.Domain.Enums;

namespace ApprovalService.Domain.Entities
{
    /// <summary>A change waiting for an admin, with its action stored as JSON and run once approved.</summary>
    public class ApprovalRequest
    {
        public int Id { get;private set; }
        public string RequestType { get; private set; } = string.Empty;
        public string EntityType { get; private set; } = string.Empty;
        public int? EntityId { get; private set; }
        public string ActionData { get;private set; } = string.Empty;
        public int RequestedById { get; private set; }
        public string RequestedByName { get; private set; }= string.Empty;
        public int? ApprovedById { get; private set; }
        public string? ApprovedByName { get; private set; }
        public ApprovalStatus Status {  get; private set; }
        public string? RejectionReason { get; private set; }
        public DateTime CreatedAt {  get; private set; }
        public DateTime? ProcessedAt {  get; private set; }
        public DateTime? ExecutedAt { get; private set; }

        protected ApprovalRequest() { }

        public ApprovalRequest(
            string requestType,
            string entityType,
            int? entityId,
            string actionData,
            int requestedById,
            string requestedByName)
        {
            RequestType = requestType;
            EntityType = entityType;
            EntityId = entityId;
            ActionData = actionData;
            RequestedById = requestedById;
            RequestedByName = requestedByName;
            Status = ApprovalStatus.Pending;
            CreatedAt = DateTime.Now;
        }

        public void Approve(int approvedById,string approvedByName)
        {
            if(Status!=ApprovalStatus.Pending)
                throw new InvalidOperationException("Only pending requests can be approved");

            ApprovedById = approvedById;
            ApprovedByName = approvedByName;
            Status = ApprovalStatus.Approved;
            ProcessedAt= DateTime.Now;
        }

        public void Reject(int rejectedById,string rejectedByName,string reason)
        {
            if (Status != ApprovalStatus.Pending)
                throw new InvalidOperationException("Only pending requests can be rejected");

            ApprovedById = rejectedById;
            ApprovedByName = rejectedByName;
            Status = ApprovalStatus.Rejected;
            RejectionReason = reason;
            ProcessedAt = DateTime.Now;
        }
        public void MarkAsExecuted()
        {
            if (Status != ApprovalStatus.Approved)
                throw new InvalidOperationException("Only approved requests can be marked as executed");

            Status = ApprovalStatus.Executed;
            ExecutedAt = DateTime.Now;
        }
        public void MarkAsFailed(string reason)
        {
            if (Status != ApprovalStatus.Approved)
                throw new InvalidOperationException("Only approved requests can be marked as failed");

            Status = ApprovalStatus.Failed;
            RejectionReason = reason;
        }
    }
}