using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class Patient
    {
        public int Id { get; set; }
        [ForeignKey("UserId")]
        public Guid UserId { get; set; }
        public required User User { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }

    }
}
