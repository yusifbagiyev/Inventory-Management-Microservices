namespace ApprovalService.Domain.Enums
{
    /// <summary>Approved only lasts until the action has run and the request becomes Executed or Failed.</summary>
    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Executed = 4,
        Failed = 5
    }
}