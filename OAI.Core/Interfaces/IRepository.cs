using System.Linq.Expressions;
using OAI.Core.Entities;

namespace OAI.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(int id, Func<IQueryable<T>, IQueryable<T>> include);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetAsync(
        Expression<Func<T, bool>> filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
        Func<IQueryable<T>, IQueryable<T>> include = null,
        int? skip = null,
        int? take = null);
    Task<T> AddAsync(T entity);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    void Update(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync();
    IQueryable<T> Query();
}