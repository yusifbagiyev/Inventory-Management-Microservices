using ProductService.Application.Interfaces;

namespace ProductService.Application.Services
{
    /// <summary>Runs an operation and calls its compensation when it throws, without any database transaction.</summary>
    public class TransactionService : ITransactionService
    {
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task> compensate)
        {
            try
            {
                return await operation();
            }
            catch
            {
                await compensate();
                throw;
            }
        }
    }
}