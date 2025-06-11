using OAI.Core.Entities;

namespace OAI.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task<int> CommitAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    IRepository<T> GetRepository<T>() where T : BaseEntity;
    IGuidRepository<T> GetGuidRepository<T>() where T : BaseGuidEntity;
}