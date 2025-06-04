namespace OAI.Core.DTOs;

public abstract class UpdateDtoBase
{
    public int Id { get; set; }
    // Base class pro UPDATE DTOs - s Id, ale bez CreatedAt, UpdatedAt
}