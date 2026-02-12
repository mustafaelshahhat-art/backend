using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes);
    Task<T?> GetByIdAsync(Guid id, string[] includePaths);
    Task<T?> GetByIdNoTrackingAsync(Guid id, string[] includePaths);
    Task<T?> GetByIdNoTrackingAsync(Guid id, params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> GetAllAsync(string[] includePaths);
    Task<IEnumerable<T>> GetAllNoTrackingAsync(string[] includePaths);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includePaths);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, string[] includePaths);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task UpdateRangeAsync(IEnumerable<T> entities);
    Task DeleteAsync(T entity);
    Task DeleteAsync(Guid id);
    Task DeleteRangeAsync(IEnumerable<T> entities);
    Task HardDeleteAsync(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters);
    Task<long> SumAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector);
    Task<IEnumerable<string>> GetDistinctAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, string?>> selector);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
