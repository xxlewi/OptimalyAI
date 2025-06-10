using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OAI.Core.Entities;

namespace OAI.Core.Interfaces
{
    /// <summary>
    /// Repository interface for entities with GUID primary key
    /// </summary>
    public interface IGuidRepository<T> where T : BaseGuidEntity
    {
        Task<T?> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> CreateAsync(T entity);
        Task<T> UpdateAsync(T entity);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync();
        
        // Advanced query methods
        IQueryable<T> GetAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IQueryable<T>>? include = null,
            int? skip = null,
            int? take = null);
    }
}