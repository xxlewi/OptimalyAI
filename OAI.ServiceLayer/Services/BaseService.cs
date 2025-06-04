using System.Linq.Expressions;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;

namespace OAI.ServiceLayer.Services;

public abstract class BaseService<T> : IBaseService<T> where T : BaseEntity
{
    protected readonly IRepository<T> _repository;
    protected readonly IUnitOfWork _unitOfWork;

    protected BaseService(IRepository<T> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _repository.FindAsync(predicate);
    }

    public virtual async Task<T> CreateAsync(T entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var result = await _repository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return result;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        var result = await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return result;
    }

    public virtual async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _repository.ExistsAsync(predicate);
    }

    public virtual async Task<int> CountAsync()
    {
        return await _repository.CountAsync();
    }
}