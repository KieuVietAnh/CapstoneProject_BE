using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        IQueryable<T> Entities { get; }

        // Non-async methods
        #region Get
        /// <summary>
        /// Retrieves all records from the database synchronously.
        /// </summary>
        /// <returns>A list of all records.</returns>
        IEnumerable<T> GetAll();

        /// <summary>
        /// Retrieves a single record from the database synchronously based on its ID.
        /// </summary>
        /// <param name="id">The ID of the record to retrieve.</param>
        /// <returns>The record corresponding to the provided ID, or null if not found.</returns>
        T? GetById(object id);

        /// <summary>
        /// Finds records that match the specified condition synchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <returns>A list of records that match the condition.</returns>
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Finds records that match the specified condition and sorts them synchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="orderBy">The sorting expression.</param>
        /// <returns>A sorted list of records that match the condition.</returns>
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy);

        /// <summary>
        /// Finds records that match the specified condition, sorts them, and projects them into a specified type synchronously.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="orderBy">The sorting expression.</param>
        /// <param name="selector">The projection expression.</param>
        /// <returns>A list of projected records that match the condition.</returns>
        IEnumerable<TResult> Find<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector);

        /// <summary>
        /// Finds records that match the specified condition, sorts them, and projects them into a specified type with pagination synchronously.
        /// </summary>
        /// <typeparam name="TResult">  </typeparam>
        /// <param name="predicate">  </param>
        /// <param name="orderBy"></param>
        /// <param name="selector"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        IEnumerable<TResult> Find<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector,
            int pageIndex = 0,
            int pageSize = 10);

        ///// <summary>
        ///// Finds records with pagination synchronously.
        ///// </summary>
        ///// <param name="predicate">The condition to filter records.</param>
        ///// <param name="orderBy">The sorting expression.</param>
        ///// <param name="pageNumber">The page number to retrieve.</param>
        ///// <param name="pageSize">The number of records per page.</param>
        ///// <returns>A paged result containing the records and pagination details.</returns>
        //PagedResult<T> FindWithPaging(
        //    Expression<Func<T, bool>>? predicate,
        //    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
        //    int pageNumber,
        //    int pageSize);
        #endregion

        /// <summary>
        /// Adds a new record to the database synchronously.
        /// </summary>
        /// <param name="entity">The record to add.</param>
        void Add(T entity);

        /// <summary>
        /// Adds multiple records to the database synchronously.
        /// </summary>
        /// <param name="entities">The list of records to add.</param>
        void AddRange(IEnumerable<T> entities);

        /// <summary>
        /// Updates an existing record in the database synchronously.
        /// </summary>
        /// <param name="entity">The record to update.</param>
        void Update(T entity);

        /// <summary>
        /// Deletes a record from the database synchronously.
        /// </summary>
        /// <param name="entity">The record to delete.</param>
        void Delete(T entity);

        /// <summary>
        /// Deletes multiple records from the database synchronously.
        /// </summary>
        /// <param name="entities">The list of records to delete.</param>
        void DeleteRange(IEnumerable<T> entities);

        // Async methods

        #region Get Async
        /// <summary>
        /// Retrieves all records from the database asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of all records.</returns>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Retrieves a single record from the database asynchronously based on its ID.
        /// </summary>
        /// <param name="id">The ID of the record to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the record corresponding to the provided ID, or null if not found.</returns>
        Task<T?> GetByIdAsync(object id);

        /// <summary>
        /// Finds records that match the specified condition asynchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of records that match the condition.</returns>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Finds record that match the specified condition asynchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="includes">The tables want to be connected</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of records that match the condition.</returns>
        Task<T?> FindAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>>? include = null);

        /// <summary>
        /// Finds record that match the specified condition asynchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="includes">The tables want to be connected</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of records that match the condition.</returns>
        Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IQueryable<T>>? include = null);

        /// <summary>
        /// Finds records that match the specified condition and sorts them asynchronously.
        /// </summary>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="orderBy">The sorting expression.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a sorted list of records that match the condition.</returns>
        Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy);

        /// <summary>
        /// Finds records that match the specified condition, sorts them, and projects them into a specified type asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="predicate">The condition to filter records.</param>
        /// <param name="orderBy">The sorting expression.</param>
        /// <param name="selector">The projection expression.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of projected records that match the condition.</returns>
        Task<IEnumerable<TResult>> FindAsync<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector);

        Task<IEnumerable<TResult>> FindAsync<TResult>(
            Expression<Func<T, bool>>? predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            Expression<Func<T, TResult>> selector,
            int pageIndex = 0,
            int pageSize = 10);

        ///// <summary>
        ///// Finds records with pagination asynchronously.
        ///// </summary>
        ///// <param name="predicate">The condition to filter records.</param>
        ///// <param name="orderBy">The sorting expression.</param>
        ///// <param name="pageNumber">The page number to retrieve.</param>
        ///// <param name="pageSize">The number of records per page.</param>
        ///// <returns>A task that represents the asynchronous operation. The task result contains a paged result with the records and pagination details.</returns>
        //Task<PagedResult<T>> FindWithPagingAsync(
        //    Expression<Func<T, bool>>? predicate,
        //    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
        //    int pageNumber,
        //    int pageSize);
        #endregion

        #region Add Async
        /// <summary>
        /// Adds a new record to the database asynchronously.
        /// </summary>
        /// <param name="entity">The record to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddAsync(T entity);

        /// <summary>
        /// Adds multiple records to the database asynchronously.
        /// </summary>
        /// <param name="entities">The list of records to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddRangeAsync(IEnumerable<T> entities);
        #endregion

        #region Update Async
        /// <summary>
        /// Updates an existing record in the database asynchronously.
        /// </summary>
        /// <param name="entity">The record to update.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task UpdateAsync(T entity);
        #endregion

        #region Delete Async
        /// <summary>
        /// Deletes a record from the database asynchronously.
        /// </summary>
        /// <param name="entity">The record to delete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DeleteAsync(T entity);

        /// <summary>
        /// Deletes multiple records from the database asynchronously.
        /// </summary>
        /// <param name="entities">The list of records to delete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DeleteRangeAsync(IEnumerable<T> entities);
        #endregion
    }
}
