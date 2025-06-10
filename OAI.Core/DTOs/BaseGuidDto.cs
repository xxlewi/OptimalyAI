namespace OAI.Core.DTOs;

public abstract class BaseGuidDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}