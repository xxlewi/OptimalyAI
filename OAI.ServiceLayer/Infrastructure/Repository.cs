using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OAI.Core.Entities;
using OAI.Core.Interfaces;

namespace OAI.ServiceLayer.Infrastructure;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task<T> CreateAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        // Check if entity is already being tracked
        var local = _dbSet.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (local != null)
        {
            // Entity is already being tracked, update its values
            _context.Entry(local).CurrentValues.SetValues(entity);
            return await Task.FromResult(local);
        }
        else
        {
            // Entity is not being tracked, attach and mark as modified
            _dbSet.Update(entity);
            return await Task.FromResult(entity);
        }
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public virtual async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }
}