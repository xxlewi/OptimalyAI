using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Business
{
    public class RequestNote : BaseEntity
    {
        public int RequestId { get; set; }
        public virtual Request Request { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; }

        [MaxLength(100)]
        public string? Author { get; set; }

        public NoteType Type { get; set; } = NoteType.Note;

        public bool IsInternal { get; set; } = false; // Interní poznámka nebo viditelná pro klienta
    }

    public enum NoteType
    {
        Note,        // Poznámka
        Progress,    // Pokrok
        Issue,       // Problém
        Solution     // Řešení
    }
}