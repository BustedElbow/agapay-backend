using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public enum VerificationStatus
    {
        Pending,
        Verified,
        Rejected
    }

    public class PhysicalTherapist
    {
        public int Id { get; set; }
        [ForeignKey("UserId")]
        public Guid UserId { get; set; }
        public required User User { get; set; }
        public required string LicenseNumber { get; set; }
        public string? LicenseImageUrl { get; set; } //This is temporary so might be removed
        public string? WorkPhoneNumber { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public required VerificationStatus VerificationStatus { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? RejectionReason { get; set; } //Might be needed

        // Physical Therapist needs experience, specialization, conditions they treat, and areas they service
        public required int YearsOfExperience { get; set; }
        public ICollection<Specialization> Specializations { get; } = new List<Specialization>();
        public ICollection<ConditionTreated> ConditionsTreated { get; } = new List<ConditionTreated>();
        public ICollection<ServiceArea> ServiceAreas { get; } = new List<ServiceArea>();

        public ICollection<TherapistAvailability> Availabilities { get; set; } = new List<TherapistAvailability>();
        public bool IsOnboardingComplete { get; set; }

        // Rating aggregates (denormalized for fast reads)
        // AverageRating is null when no ratings exist
        public double? AverageRating { get; set; }
        public int RatingCount { get; set; }

        // Optional: therapist fee (used for budget normalization if you add budget matching)
        public decimal? FeePerSession { get; set; }
    }
}
