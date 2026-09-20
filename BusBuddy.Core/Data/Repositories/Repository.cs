using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Generic repository implementation for common CRUD operations
/// Supports both async and sync operations for Syncfusion compatibility
/// Includes soft delete functionality and audit tracking
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    protected BusBuddyDbContext Context { get; }
    protected DbSet<T> DbSet { get; }
    protected IUserContextService UserContextService { get; }
    private readonly bool _supportsSoftDelete;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public Repository(BusBuddyDbContext context, IUserContextService userContextService)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        UserContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
        DbSet = Context.Set<T>();
        _supportsSoftDelete = typeof(T).GetProperty("Active")?.PropertyType == typeof(bool);
    }

    #region Async Query Operations

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        try
        {
            var entity = await DbSet.FindAsync(id);

            if (entity != null && _supportsSoftDelete)
            {
                var activeProperty = typeof(T).GetProperty("Active");
                if (activeProperty?.PropertyType == typeof(bool))
                {
                    var isActive = (bool)activeProperty.GetValue(entity)!;
                    if (!isActive)
                    {
                        return null; // Entity is marked inactive
                    }
                }
            }

            return entity;
        }
        catch (Exception)
        {
            // Log exception if needed and return null for safe fallback
            return null;
        }
    }

    public virtual async Task<T?> GetByIdAsync(object id)
    {
        var entity = await DbSet.FindAsync(id);

        if (entity != null && _supportsSoftDelete)
        {
            var activeProperty = typeof(T).GetProperty("Active");
            if (activeProperty?.PropertyType == typeof(bool))
            {
                var isActive = (bool)activeProperty.GetValue(entity)!;
                if (!isActive)
                {
                    return null; // Entity is marked inactive
                }
            }
        }

        return entity;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // Create a query with appropriate filters
            IQueryable<T> query = QueryNoTracking();

            // Materialize the query to avoid context sharing issues
            var result = await query.ToListAsync();
            return result ?? new List<T>(); // Ensure never null
        }
        catch (Exception)
        {
            // Return empty list on error for safe fallback
            return new List<T>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return await query.ToListAsync();
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return await query.FirstOrDefaultAsync();
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return await query.AnyAsync();
    }

    public virtual async Task<int> CountAsync()
    {
        IQueryable<T> query = DbSet;


        return await query.CountAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return await query.CountAsync();
    }

    #endregion

    #region Synchronous Query Operations

    public virtual T? GetById(int id)
    {
        return DbSet.Find(id);
    }

    public virtual T? GetById(object id)
    {
        return DbSet.Find(id);
    }

    public virtual IQueryable<T> GetAllQueryable()
    {
        IQueryable<T> query = DbSet;


        return query;
    }

    public virtual IEnumerable<T> GetAll()
    {
        return GetAllQueryable().ToList();
    }

    public virtual IEnumerable<T> Find(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return query.ToList();
    }

    public virtual T? FirstOrDefault(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return query.FirstOrDefault();
    }

    public virtual bool Any(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return query.Any();
    }

    public virtual int Count()
    {
        IQueryable<T> query = DbSet;


        return query.Count();
    }

    public virtual int Count(Expression<Func<T, bool>> expression)
    {
        IQueryable<T> query = DbSet.Where(expression);


        return query.Count();
    }

    #endregion

    #region Modification Operations

    public virtual async Task<T> AddAsync(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        SetAuditFields(entity, isUpdate: false);
        var result = await DbSet.AddAsync(entity);
        return result.Entity;
    }

    public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        var entityList = entities.ToList();
        foreach (var entity in entityList)
        {
            SetAuditFields(entity, isUpdate: false);
        }
        await DbSet.AddRangeAsync(entityList);
        return entityList;
    }

    public virtual T Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        SetAuditFields(entity, isUpdate: false);
        var result = DbSet.Add(entity);
        return result.Entity;
    }

    public virtual void AddRange(IEnumerable<T> entities)
    {
        var entityList = entities.ToList();
        foreach (var entity in entityList)
        {
            SetAuditFields(entity, isUpdate: false);
        }
        DbSet.AddRange(entityList);
    }

    public virtual void Update(T entity)
    {
        SetAuditFields(entity, isUpdate: true);
        DbSet.Update(entity);
    }

    public virtual void UpdateRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            SetAuditFields(entity, isUpdate: true);
        }
        DbSet.UpdateRange(entities);
    }

    public virtual void Remove(T entity)
    {
        DbSet.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        DbSet.RemoveRange(entities);
    }

    public virtual async Task<bool> RemoveByIdAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        Remove(entity);
        return true;
    }

    public virtual async Task<bool> RemoveByIdAsync(object id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        Remove(entity);
        return true;
    }

    #endregion

    #region Soft Delete Operations

    public virtual async Task<bool> SoftDeleteAsync(int id)
    {
        if (!_supportsSoftDelete)
        {
            return false;
        }

        var entity = await GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        SoftDelete(entity);
        return true;
    }

    public virtual async Task<bool> SoftDeleteAsync(object id)
    {
        if (!_supportsSoftDelete)
        {
            return false;
        }

        var entity = await GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        SoftDelete(entity);
        return true;
    }

    public virtual void SoftDelete(T entity)
    {
        // Per-aggregate Active (Student/Driver); no shared BaseEntity.IsDeleted.
        var activeProperty = typeof(T).GetProperty("Active");
        if (activeProperty != null && activeProperty.PropertyType == typeof(bool))
        {
            activeProperty.SetValue(entity, false);
            SetAuditFields(entity, isUpdate: true);
            DbSet.Update(entity);
            return;
        }

        // No soft delete support
    }

    public virtual async Task RestoreAsync(int id)
    {
        if (!_supportsSoftDelete)
        {
            return;
        }

        var entity = await DbSet.FindAsync(id);
        if (entity == null)
        {
            return;
        }

        Restore(entity);
    }

    public virtual async Task RestoreAsync(object id)
    {
        if (!_supportsSoftDelete)
        {
            return;
        }

        var entity = await DbSet.FindAsync(id);
        if (entity == null)
        {
            return;
        }

        Restore(entity);
    }

    public virtual void Restore(T entity)
    {

        // Handle Student/Driver pattern (Active property)
        var activeProperty = typeof(T).GetProperty("Active");
        if (activeProperty != null && activeProperty.PropertyType == typeof(bool))
        {
            activeProperty.SetValue(entity, true);
            SetAuditFields(entity, isUpdate: true);
            DbSet.Update(entity);
            return;
        }

        // No soft delete support
    }

    #endregion

    #region Pagination Support

    public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        IQueryable<T> query = DbSet;


        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
    {
        IQueryable<T> query = DbSet;


        if (filter != null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync();

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    #endregion

    #region Advanced Querying

    public virtual IQueryable<T> Query()
    {
        IQueryable<T> query = DbSet;

        if (_supportsSoftDelete && typeof(T).GetProperty("Active")?.PropertyType == typeof(bool))
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            var property = Expression.Property(parameter, "Active");
            var equal = Expression.Equal(property, Expression.Constant(true));
            query = query.Where(Expression.Lambda<Func<T, bool>>(equal, parameter));
        }

        return query;
    }

    public virtual IQueryable<T> QueryNoTracking()
    {
        IQueryable<T> query = DbSet.AsNoTracking();

        if (_supportsSoftDelete && typeof(T).GetProperty("Active")?.PropertyType == typeof(bool))
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            var property = Expression.Property(parameter, "Active");
            var equal = Expression.Equal(property, Expression.Constant(true));
            query = query.Where(Expression.Lambda<Func<T, bool>>(equal, parameter));
        }

        return query;
    }

    public virtual async Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector)
    {
        IQueryable<T> query = DbSet;


        return await query.Select(selector).ToListAsync();
    }

    public virtual async Task<IEnumerable<TResult>> SelectAsync<TResult>(
        Expression<Func<T, bool>> filter,
        Expression<Func<T, TResult>> selector)
    {
        IQueryable<T> query = DbSet.Where(filter);


        return await query.Select(selector).ToListAsync();
    }

    #endregion

    #region Helper Methods

    private void SetAuditFields(T entity, bool isUpdate)
    {
        var currentUser = GetCurrentUser();
        var currentTime = DateTime.UtcNow;

        // Per-aggregate audit fields when present (Student/Driver).
        var entityType = typeof(T);

        if (!isUpdate)
        {
            var createdDateProp = entityType.GetProperty("CreatedDate");
            var createdByProp = entityType.GetProperty("CreatedBy");

            if (createdDateProp?.PropertyType == typeof(DateTime))
            {
                createdDateProp.SetValue(entity, currentTime);
            }

            if (createdByProp?.PropertyType == typeof(string))
            {
                createdByProp.SetValue(entity, currentUser);
            }
        }
        else
        {
            var updatedDateProp = entityType.GetProperty("UpdatedDate");
            var updatedByProp = entityType.GetProperty("UpdatedBy");

            if (updatedDateProp?.PropertyType == typeof(DateTime?))
            {
                updatedDateProp.SetValue(entity, currentTime);
            }

            if (updatedByProp?.PropertyType == typeof(string))
            {
                updatedByProp.SetValue(entity, currentUser);
            }
        }
    }

    private string? GetCurrentUser()
    {
        // Get the current authenticated user from the user context service
        return UserContextService.GetCurrentUserForAudit();
    }

    #endregion
}
