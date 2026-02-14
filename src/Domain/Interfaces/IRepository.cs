using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, Expression<Func<T, object>>[] includes, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, string[] includePaths, CancellationToken ct = default);
    Task<T?> GetByIdNoTrackingAsync(Guid id, string[] includePaths, CancellationToken ct = default);
    Task<T?> GetByIdNoTrackingAsync(Guid id, Expression<Func<T, object>>[] includes, CancellationToken ct = default);

    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters, CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includePaths, CancellationToken ct = default);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, string[] includePaths, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task HardDeleteAsync(T entity, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters, CancellationToken ct = default);
    Task<long> SumAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default);
    Task<IEnumerable<string>> GetDistinctAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, string?>> selector, CancellationToken ct = default);
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, CancellationToken ct = default, params Expression<Func<T, object>>[] includes);
    
    // Performance Optimization
    IQueryable<T> GetQueryable();
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
