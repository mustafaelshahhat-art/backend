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
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includePaths);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, string[] includePaths);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task DeleteAsync(Guid id);
    Task HardDeleteAsync(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
