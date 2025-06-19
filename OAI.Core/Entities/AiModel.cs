using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities;

public class AiModel : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? FilePath { get; set; } // Cesta k modelu na disku
    
    public long SizeBytes { get; set; }
    
    [StringLength(50)]
    public string? Tag { get; set; }
    
    [StringLength(100)]
    public string? Family { get; set; }
    
    [StringLength(50)]
    public string? ParameterSize { get; set; }
    
    [StringLength(50)]
    public string? QuantizationLevel { get; set; }
    
    public DateTime? LastUsedAt { get; set; }
    
    public bool IsAvailable { get; set; } = true; // Jestli je model dostupný k použití
    
    public bool IsDefault { get; set; } = false;
    
    // Vztah k serveru
    public Guid AiServerId { get; set; }
    public virtual AiServer AiServer { get; set; } = null!;
    
    // Metadata
    [StringLength(2000)]
    public string? Metadata { get; set; } // JSON s dodatečnými informacemi
}