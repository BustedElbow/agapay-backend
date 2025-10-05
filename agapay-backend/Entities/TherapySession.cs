using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
  public enum SessionStatus
  {
    Scheduled,
    Completed,
    Cancelled
  }

  public enum CancellationInitiator
  {
    Patient,
    Therapist
  }

  public class TherapySession
  {
    public int Id { get; set; }

    [ForeignKey("PatientId")]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [ForeignKey("PhysicalTherapistId")]
    public int PhysicalTherapistId { get; set; }
    public PhysicalTherapist? PhysicalTherapist { get; set; }

    // Contract linkage (parent aggregate)
    [ForeignKey("ContractId")]
    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    // Location of the patient for the session (address / display)
    public string? LocationAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Scheduling
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int DurationMinutes { get; set; }

    // Doctor's referral image (optional)
    public string? DoctorReferralImageUrl { get; set; }

    // Cost breakdown
    public decimal TotalFee { get; set; }
    public decimal PatientFee { get; set; } // portion patient needs to pay

    // State
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public string? CancellationReason { get; set; }
    public CancellationInitiator? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
}