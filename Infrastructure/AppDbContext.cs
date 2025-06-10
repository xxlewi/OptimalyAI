using Microsoft.EntityFrameworkCore;
using OAI.Core.Entities;
using OAI.Core.Entities.Business;
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
        // Configure Conversation and Message relationship
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Configure Business entities
        ConfigureBusinessEntities(modelBuilder);
    }
    
    private void ConfigureBusinessEntities(ModelBuilder modelBuilder)
    {
        // BusinessRequest configuration
        modelBuilder.Entity<BusinessRequest>()
            .HasIndex(br => br.RequestNumber)
            .IsUnique();
            
        modelBuilder.Entity<BusinessRequest>()
            .Property(br => br.RequestNumber)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("CONCAT('REQ-', YEAR(GETDATE()), '-', FORMAT(NEXT VALUE FOR RequestNumberSequence, '0000'))");
            
        // Create sequence for request numbers
        modelBuilder.HasSequence<int>("RequestNumberSequence")
            .StartsAt(1)
            .IncrementsBy(1);
            
        // WorkflowTemplate configuration
        modelBuilder.Entity<WorkflowTemplate>()
            .HasIndex(wt => new { wt.Name, wt.Version })
            .IsUnique();
            
        // WorkflowStep configuration
        modelBuilder.Entity<WorkflowStep>()
            .HasIndex(ws => new { ws.WorkflowTemplateId, ws.Order });
            
        // RequestExecution configuration
        modelBuilder.Entity<RequestExecution>()
            .HasOne(re => re.Conversation)
            .WithMany()
            .HasForeignKey(re => re.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // StepExecution configuration
        modelBuilder.Entity<StepExecution>()
            .HasOne(se => se.ToolExecution)
            .WithMany()
            .HasForeignKey(se => se.ToolExecutionId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // RequestFile configuration
        modelBuilder.Entity<RequestFile>()
            .HasIndex(rf => rf.StoragePath);
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