namespace ApprovalService.Application.Interfaces
{
    /// <summary>Runs the stored action of an approved request in the service that owns it.</summary>
    public interface IActionExecutor
    {
        Task<bool> ExecuteAsync(
            string requestType, 
            string actionData, 
            int userId,
            string userName,
            CancellationToken cancellationToken = default);
    }
}