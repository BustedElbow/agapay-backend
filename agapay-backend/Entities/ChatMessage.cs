using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class ChatMessage
    {
        public int Id { get; set; }

        // The user who sent the message
        [ForeignKey("SenderId")]
        public Guid SenderId { get; set; }
        public User Sender { get; set; }

        // The user who should receive the message
        [ForeignKey("ReceiverId")]
        public Guid ReceiverId { get; set; }
        public User Receiver { get; set; }

        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }
}