using OAI.Core.Entities;

namespace OAI.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    IRepository<T> GetRepository<T>() where T : BaseEntity;
}