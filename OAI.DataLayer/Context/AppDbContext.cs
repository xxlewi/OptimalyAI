using Microsoft.EntityFrameworkCore;
using OAI.Core.Entities;
using OAI.Core.Entities.Business;
using OAI.Core.Entities.Projects;
using OAI.Core.Entities.Adapters;
using OAI.DataLayer.Extensions;
using System.Reflection;

namespace OAI.DataLayer.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Suppress pending model changes warning - database is manually synchronized
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    // DbSets se přidají automaticky pro všechny entity dědící z BaseEntity
    // Nebo můžete přidat explicitně:
    // public DbSet<YourEntity> YourEntities { get; set; }
    
    // Explicit DbSets for workflow entities
    public DbSet<ProjectStage> ProjectStages { get; set; }
    public DbSet<ProjectStageTool> ProjectStageTools { get; set; }
    
    // Adapter entities
    public DbSet<AdapterDefinition> AdapterDefinitions { get; set; }
    public DbSet<AdapterExecution> AdapterExecutions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatická registrace všech entit
        modelBuilder.RegisterEntitiesAutomatically();

        // Automatická konfigurace BaseEntity
        modelBuilder.ConfigureBaseEntities(Database.IsNpgsql());

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
        
        // Configure Workflow entities
        ConfigureWorkflowEntities(modelBuilder);
    }
    
    private void ConfigureBusinessEntities(ModelBuilder modelBuilder)
    {
        // Request configuration
        modelBuilder.Entity<Request>()
            .HasIndex(br => br.RequestNumber)
            .IsUnique();
            
        // Configure request number
        modelBuilder.Entity<Request>()
            .Property(br => br.RequestNumber)
            .HasMaxLength(50);

        // Configure Request -> Project relationship
        modelBuilder.Entity<Request>()
            .HasOne(br => br.Project)
            .WithMany(p => p.Requests)
            .HasForeignKey(br => br.ProjectId)
            .OnDelete(DeleteBehavior.SetNull); // Když se smaže projekt, nastaví se ProjectId na NULL
            
        // Configure Request -> WorkflowTemplate relationship
        modelBuilder.Entity<Request>()
            .HasOne(r => r.WorkflowTemplate)
            .WithMany()
            .HasForeignKey(r => r.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // Sequence will be created only for PostgreSQL during migration
            
        // PostgreSQL sequence for request numbers
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
    
    private void ConfigureWorkflowEntities(ModelBuilder modelBuilder)
    {
        // ProjectStage configuration
        modelBuilder.Entity<ProjectStage>()
            .HasOne(ps => ps.Project)
            .WithMany(p => p.Stages)
            .HasForeignKey(ps => ps.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<ProjectStage>()
            .HasIndex(ps => new { ps.ProjectId, ps.Order });
            
        // ProjectStageTool configuration
        modelBuilder.Entity<ProjectStageTool>()
            .HasOne(pst => pst.ProjectStage)
            .WithMany(ps => ps.StageTools)
            .HasForeignKey(pst => pst.ProjectStageId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<ProjectStageTool>()
            .HasIndex(pst => new { pst.ProjectStageId, pst.Order });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatické nastavení CreatedAt pro nové BaseEntity
        var newBaseEntries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in newBaseEntries)
        {
            entry.Entity.CreatedAt = DateTime.UtcNow;
        }

        // Automatické nastavení UpdatedAt pro modifikované BaseEntity
        var baseEntries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in baseEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        // Automatické nastavení CreatedAt pro nové BaseGuidEntity
        var newGuidEntries = ChangeTracker.Entries<BaseGuidEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in newGuidEntries)
        {
            entry.Entity.CreatedAt = DateTime.UtcNow;
        }

        // Automatické nastavení UpdatedAt pro modifikované BaseGuidEntity
        var guidEntries = ChangeTracker.Entries<BaseGuidEntity>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in guidEntries)
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