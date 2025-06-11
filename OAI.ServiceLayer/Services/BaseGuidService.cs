using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;

namespace OAI.ServiceLayer.Services
{
    /// <summary>
    /// Base service for entities with GUID primary key
    /// </summary>
    public abstract class BaseGuidService<T> : IBaseGuidService<T> where T : BaseGuidEntity
    {
        protected readonly IUnitOfWork _unitOfWork;

        protected BaseGuidService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        protected IGuidRepository<T> Repository => _unitOfWork.GetGuidRepository<T>();

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            return await Repository.GetByIdAsync(id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await Repository.GetAllAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FindAsync(predicate);
        }

        public virtual async Task<T> CreateAsync(T entity)
        {
            await Repository.AddAsync(entity);
            await _unitOfWork.CommitAsync();
            return entity;
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            Repository.Update(entity);
            await _unitOfWork.CommitAsync();
            return entity;
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            await Repository.DeleteAsync(id);
            await _unitOfWork.CommitAsync();
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await Repository.ExistsAsync(predicate);
        }

        public virtual async Task<int> CountAsync()
        {
            return await Repository.CountAsync();
        }
    }
}