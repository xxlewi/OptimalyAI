using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces;
using OAI.Core.Entities;
using OAI.DataLayer.Repositories;
using System.Collections.Generic;

namespace OAI.DataLayer.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(DbContext context, IServiceProvider serviceProvider, ILogger<UnitOfWork> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync()
    {
        var result = await _context.SaveChangesAsync();
        _logger.LogDebug("SaveChangesAsync called, {Count} entities affected", result);
        return result;
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public IRepository<T> GetRepository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(Repository<>).MakeGenericType(type);
            var repository = Activator.CreateInstance(repositoryType, _context);
            _repositories[type] = repository!;
        }
        return (IRepository<T>)_repositories[type];
    }

    public IGuidRepository<T> GetGuidRepository<T>() where T : BaseGuidEntity
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GuidRepository<>).MakeGenericType(type);
            var repository = Activator.CreateInstance(repositoryType, _context);
            _repositories[type] = repository!;
        }
        return (IGuidRepository<T>)_repositories[type];
    }

    public async Task<int> CommitAsync()
    {
        return await SaveChangesAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}