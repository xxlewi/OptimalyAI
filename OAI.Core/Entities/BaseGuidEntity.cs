using System;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Base entity with GUID as primary key
    /// </summary>
    public abstract class BaseGuidEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}