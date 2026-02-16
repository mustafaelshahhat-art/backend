using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
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

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public async Task<T?> GetByIdAsync(Guid id, Expression<Func<T, object>>[] includes, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T?> GetByIdAsync(Guid id, string[] includePaths, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T?> GetByIdNoTrackingAsync(Guid id, string[] includePaths, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T?> GetByIdNoTrackingAsync(Guid id, Expression<Func<T, object>>[] includes, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }



    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(predicate, ct);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return await query.AnyAsync(predicate, ct);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(ct);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, bool ignoreFilters, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return await query.Where(predicate).ToListAsync(ct);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includePaths, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.Where(predicate).ToListAsync(ct);
    }

    public async Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    public async Task<IEnumerable<T>> GetNoTrackingAsync(Expression<Func<T, bool>> predicate, string[] includePaths, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }
        return await query.Where(predicate).ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _dbSet.AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        _dbSet.UpdateRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        _dbSet.RemoveRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity != null)
        {
            await DeleteAsync(entity, ct);
        }
    }

    public async Task HardDeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(predicate, ct);
    }

    public async Task<long> SumAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).SumAsync(selector, ct);
    }

    public async Task<IEnumerable<string>> GetDistinctAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, string?>> selector, CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).Select(selector).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).Distinct().ToListAsync(ct);
    }

    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, CancellationToken ct = default, params Expression<Func<T, object>>[] includes)
    {
        // PROD-AUDIT: Enforce page bounds — cap at 500 to support large tournaments
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 500) pageSize = 500;

        IQueryable<T> query = _dbSet.AsNoTracking();

        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        int totalCount = await query.CountAsync(ct);

        if (orderBy != null)
        {
            query = orderBy(query);
        }
        else
        {
            // Default ordering is required for consistency
            query = query.OrderBy(e => e.Id);
        }

        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return (items, totalCount);
    }

    public IQueryable<T> GetQueryable()
    {
        return _dbSet.AsNoTracking();
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CommitTransactionAsync(ct);
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.RollbackTransactionAsync(ct);
        }
    }
}
