using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class PhysicalTherapist
    {
        public int Id { get; set; }
        [ForeignKey("UserId")]
        public Guid UserId { get; set; }
        public required User User { get; set; }
        public required string LicenseNumber { get; set; }
        public string? WorkPhoneNumber { get; set; }
        public required string VerificationStatus { get; set; }
    }
}
