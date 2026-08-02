using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UrbanService.DAL.Data;
using UrbanService.DAL.Interfaces;
using UrbanService.DAL.Repositories;

namespace UrbanService.DAL.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly UrbanServiceDbContext _context;
        private IDbContextTransaction? _transaction;
        private readonly Dictionary<Type, object> _repositories;
        

        public UnitOfWork(UrbanServiceDbContext context)
        {
            _context = context;
            _repositories = [];
        }

        

        // Trả về repository động và đảm bảo chỉ có một instance
        public IGenericRepository<T> GetRepository<T>() where T : class
        {
            var type = typeof(T);

            // Kiểm tra xem repository đã tồn tại chưa
            if (_repositories.ContainsKey(type))
            {
                return (IGenericRepository<T>)_repositories[type];
            }

            // Nếu chưa tồn tại, tạo mới và lưu vào Dictionary
            var repository = new GenericRepository<T>(_context);
            _repositories.Add(type, repository);

            return (IGenericRepository<T>)repository;
        }

        // Lưu thay đổi đồng bộ
        public void Save()
        {
            _context.SaveChanges();
        }

        // Lưu thay đổi bất đồng bộ
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        // Bắt đầu giao dịch
        public void BeginTransaction()
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("A transaction is already active for this unit of work.");
            }

            _transaction = _context.Database.BeginTransaction();
        }

        public async Task AcquireTransactionAdvisoryLockAsync(long lockKey)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException(
                    "A transaction must be active before acquiring a transaction advisory lock.");
            }

            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0});",
                lockKey);
        }

        // Commit giao dịch
        public void CommitTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction.Dispose();
                _transaction = null;
            }
        }

        // Rollback giao dịch
        public void RollBack()
        {
            if (_transaction != null)
            {
                try
                {
                    _transaction.Rollback();
                }
                finally
                {
                    _transaction.Dispose();
                    _transaction = null;
                    _context.ChangeTracker.Clear();
                }
            }
        }

        // Dispose để giải phóng tài nguyên
        public void Dispose()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
            _context.Dispose();
        }
    }
}
