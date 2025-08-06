using System.Linq.Expressions;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Generic repository interface for common CRUD operations
/// Supports async operations for better performance with Syncfusion controls
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    // Query operations
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(object id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression);
    Task<bool> AnyAsync(Expression<Func<T, bool>> expression);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> expression);

    // Synchronous query operations for Syncfusion compatibility
    T? GetById(int id);
    T? GetById(object id);
    IQueryable<T> GetAllQueryable();
    IEnumerable<T> GetAll();
    IEnumerable<T> Find(Expression<Func<T, bool>> expression);
    T? FirstOrDefault(Expression<Func<T, bool>> expression);
    bool Any(Expression<Func<T, bool>> expression);
    int Count();
    int Count(Expression<Func<T, bool>> expression);

    // Modification operations
    Task<T> AddAsync(T entity);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities);
    T Add(T entity);
    void AddRange(IEnumerable<T> entities);

    void Update(T entity);
    void UpdateRange(IEnumerable<T> entities);

    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<bool> RemoveByIdAsync(int id);
    Task<bool> RemoveByIdAsync(object id);

    // Soft delete operations
    Task<bool> SoftDeleteAsync(int id);
    Task<bool> SoftDeleteAsync(object id);
    void SoftDelete(T entity);
    Task RestoreAsync(int id);
    Task RestoreAsync(object id);
    void Restore(T entity);

    // Pagination support for large datasets
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null);

    // Advanced querying
    IQueryable<T> Query();
    IQueryable<T> QueryNoTracking();
    Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector);
    Task<IEnumerable<TResult>> SelectAsync<TResult>(
        Expression<Func<T, bool>> filter,
        Expression<Func<T, TResult>> selector);
}
