using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public enum ContractStatus
    {
        Active,
        Completed,
        Cancelled,
        Expired
    }

    public class Contract
    {
        public int Id { get; set; }

        [ForeignKey("PatientId")] 
        public int PatientId { get; set; }
        public Patient? Patient { get; set; }

        [ForeignKey("PhysicalTherapistId")] 
        public int PhysicalTherapistId { get; set; }
        public PhysicalTherapist? PhysicalTherapist { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ContractStatus Status { get; set; } = ContractStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<TherapySession> Sessions { get; set; } = new List<TherapySession>();
    }
}
