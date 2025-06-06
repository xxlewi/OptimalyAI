using Microsoft.EntityFrameworkCore;
using OAI.Core.Entities;
using OptimalyAI.Extensions;
using System.Reflection;

namespace OptimalyAI.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets se přidají automaticky pro všechny entity dědící z BaseEntity
    // Nebo můžete přidat explicitně:
    // public DbSet<YourEntity> YourEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatická registrace všech entit
        modelBuilder.RegisterEntitiesAutomatically();

        // Automatická konfigurace BaseEntity
        modelBuilder.ConfigureBaseEntities();

        // Aplikuje konvence pro názvy
        modelBuilder.ApplyNamingConventions();

        // Aplikuje konfigurace entit ze všech assembly
        modelBuilder.ApplyConfigurationsFromAssemblies(
            typeof(AppDbContext).Assembly,           // Current assembly
            typeof(BaseEntity).Assembly,             // OAI.Core
            Assembly.Load("OAI.ServiceLayer")        // ServiceLayer
        );

        // Specifické konfigurace můžete přidat zde
        ConfigureSpecificEntities(modelBuilder);
    }

    private void ConfigureSpecificEntities(ModelBuilder modelBuilder)
    {
        // Zde můžete přidat specifické konfigurace pro jednotlivé entity
        // Například:
        // modelBuilder.Entity<User>()
        //     .HasIndex(u => u.Email)
        //     .IsUnique();
        
        // Configure Conversation and Message relationship
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatické nastavení UpdatedAt pro modifikované entity
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Automaticky vytvoří DbSet property pro daný typ entity
    /// </summary>
    public DbSet<T> GetDbSet<T>() where T : BaseEntity
    {
        return Set<T>();
    }
}