namespace UrbanService.DAL.Interfaces
{
    public interface IUnitOfWork
    {
        IGenericRepository<T> GetRepository<T>() where T : class;
        
        void Save();
        Task SaveAsync();
        void BeginTransaction();
        Task AcquireTransactionAdvisoryLockAsync(long lockKey);
        void CommitTransaction();
        void RollBack();
    }
}
