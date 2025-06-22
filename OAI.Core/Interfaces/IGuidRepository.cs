using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Specification;

namespace OAI.Core.Interfaces
{
    /// <summary>
    /// Repository interface for entities with GUID primary key
    /// </summary>
    public interface IGuidRepository<T> where T : BaseGuidEntity
    {
        // Basic CRUD operations
        Task<T?> GetByIdAsync(Guid id);
        Task<T?> GetByIdAsync(Guid id, params string[] includeProperties);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task<T> CreateAsync(T entity);
        Task<T> UpdateAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task DeleteAsync(Guid id);
        
        // Query operations using expressions (without IQueryable)
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
        
        // Specification pattern for complex queries
        Task<T?> GetBySpecAsync(ISpecification<T> spec);
        Task<IEnumerable<T>> ListAsync(ISpecification<T> spec);
        Task<int> CountAsync(ISpecification<T> spec);
    }
}