using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class Conversation
    {
        public int Id { get; set; }

        [ForeignKey("ParticipantAId")]
        public Guid ParticipantAId { get; set; }
        public User ParticipantA { get; set; }

        [ForeignKey("ParticipantBId")]
        public Guid ParticipantBId { get; set; }
        public User ParticipantB { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
