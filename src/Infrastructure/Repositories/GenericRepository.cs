using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<T?> GetByIdAsync(Guid id, string[] includePaths)
    {
        IQueryable<T> query = _dbSet;
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<T?> GetByIdNoTrackingAsync(Guid id, string[] includePaths)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<T?> GetByIdNoTrackingAsync(Guid id, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync(string[] includePaths)
    {
        IQueryable<T> query = _dbSet;
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllNoTrackingAsync(string[] includePaths)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.ToListAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters)
    {
        IQueryable<T> query = _dbSet;
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return await query.AnyAsync(predicate);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters)
    {
        IQueryable<T> query = _dbSet;
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return await query.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includePaths)
    {
        IQueryable<T> query = _dbSet;
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, string[] includePaths)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.Where(predicate).ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
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

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            await DeleteAsync(entity);
        }
    }

    public async Task HardDeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    public async Task<long> SumAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector)
    {
        return await _dbSet.Where(predicate).SumAsync(selector);
    }

    public async Task<IEnumerable<string>> GetDistinctAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, string?>> selector)
    {
        return await _dbSet.Where(predicate).Select(selector).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).Distinct().ToListAsync();
    }

    public async Task BeginTransactionAsync()
    {
        await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CommitTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.RollbackTransactionAsync();
        }
    }
}
