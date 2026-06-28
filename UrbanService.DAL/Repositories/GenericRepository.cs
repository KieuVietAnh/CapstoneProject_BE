using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UrbanService.DAL.Data;
using UrbanService.DAL.Interfaces;

namespace UrbanService.DAL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly UrbanServiceDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private const string _timeZone = "Asia/Ho_Chi_Minh";

        public GenericRepository(UrbanServiceDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public IQueryable<T> Entities => _dbSet;

        // Non-async methods
        public IEnumerable<T> GetAll()
        {
            return _dbSet.ToList();
        }

        public T? GetById(object id)
        {
            return _dbSet.Find(id);
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _dbSet.Where(predicate).ToList();
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy)
        {
            return orderBy(_dbSet.Where(predicate)).ToList();
        }

        public IEnumerable<TResult> Find<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector)
        {
            IQueryable<T> query = _dbSet;

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            return query.Select(selector).ToList();
        }

        //public PagedResult<T> FindWithPaging(
        //    Expression<Func<T, bool>>? predicate,
        //    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
        //    int pageNumber,
        //    int pageSize)
        //{
        //    IQueryable<T> query = _dbSet;

        //    if (predicate != null)
        //    {
        //        query = query.Where(predicate);
        //    }

        //    int totalItems = query.Count();

        //    if (orderBy != null)
        //    {
        //        query = orderBy(query);
        //    }

        //    var pagedData = query
        //        .Skip((pageNumber - 1) * pageSize)
        //        .Take(pageSize)
        //        .ToList();

        //    return new PagedResult<T>(pagedData, pageNumber, pageSize, totalItems);
        //}
        public void Add(T entity)
        {
            _dbSet.Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            _dbSet.AddRange(entities);
        }

        public void Update(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void DeleteRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        // Async methods
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy)
        {
            return await orderBy(_dbSet.Where(predicate)).ToListAsync();
        }

        public async Task<IEnumerable<TResult>> FindAsync<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector)
        {
            IQueryable<T> query = _dbSet;

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            return await query.Select(selector).ToListAsync();
        }

        //public async Task<PagedResult<T>> FindWithPagingAsync(
        //    Expression<Func<T, bool>>? predicate,
        //    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
        //    int pageNumber,
        //    int pageSize)
        //{
        //    IQueryable<T> query = _dbSet;

        //    if (predicate != null)
        //    {
        //        query = query.Where(predicate);
        //    }

        //    int totalItems = await query.CountAsync();

        //    if (orderBy != null)
        //    {
        //        query = orderBy(query);
        //    }

        //    var pagedData = await query
        //        .Skip((pageNumber - 1) * pageSize)
        //        .Take(pageSize)
        //        .ToListAsync();

        //    return new PagedResult<T>(pagedData, pageNumber, pageSize, totalItems);
        //}

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            await _context.SaveChangesAsync();
        }

        public IEnumerable<TResult> Find<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector,
            int pageIndex = 0, int pageSize = 10)
        {
            IQueryable<T> query = _dbSet;

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                query = orderBy(query);

            query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);

            return query.Select(selector).ToList();
        }

        public async Task<IEnumerable<TResult>> FindAsync<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector,
            int pageIndex = 0,
            int pageSize = 10)
        {
            IQueryable<T> query = _dbSet;

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                query = orderBy(query);

            query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);

            return await query.Select(selector).ToListAsync();
        }

        public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            IQueryable<T> query = _dbSet;

            if (include != null) query = include(query);

            return await query.FirstOrDefaultAsync(predicate);
        }

        public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            IQueryable<T> query = _dbSet;

            if (include != null)
            {
                query = include(query);
            }

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            return await query.ToListAsync();
        }

        public IQueryable<T> Query()
        {
            return _dbSet.AsQueryable();
        }

        public IQueryable<T> Query(
    Func<IQueryable<T>, IQueryable<T>> include)
        {
            return include(_dbSet.AsQueryable());
        }

        public async Task<int> CountAsync(
    Expression<Func<T, bool>>? predicate = null)
        {
            IQueryable<T> query = _dbSet;

            if (predicate != null)
                query = query.Where(predicate);

            return await query.CountAsync();
        }
    }
}
