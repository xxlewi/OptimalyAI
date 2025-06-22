using System.Linq.Expressions;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Specification;

namespace OAI.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    // Basic CRUD operations
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(int id, params string[] includeProperties);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    void Update(T entity);
    Task DeleteAsync(int id);
    
    // Query operations using expressions (without IQueryable)
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    
    // Specification pattern for complex queries
    Task<T?> GetBySpecAsync(ISpecification<T> spec);
    Task<IEnumerable<T>> ListAsync(ISpecification<T> spec);
    Task<int> CountAsync(ISpecification<T> spec);
    
    // Advanced query operations
    Task<IEnumerable<T>> GetAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        int? skip = null,
        int? take = null);
}