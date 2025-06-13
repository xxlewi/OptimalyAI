using System;
using OAI.Core.Entities.Business;

namespace OAI.Core.DTOs.Business
{
    public class RequestNoteDto : BaseDto
    {
        public int BusinessRequestId { get; set; }
        public string Content { get; set; }
        public string Author { get; set; }
        public NoteType Type { get; set; }
        public bool IsInternal { get; set; }
    }
}