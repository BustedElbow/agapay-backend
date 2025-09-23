using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class PatientPreferences
    {
        public int Id { get; set; }

        [ForeignKey("PatientId")]
        public int PatientId { get; set; }
        public required Patient Patient { get; set; }

        // Preferred time slots (similar structure to therapist availability)
        public DayOfWeekEnum? PreferredDayOfWeek { get; set; }
        public TimeOnly? PreferredStartTime { get; set; }
        public TimeOnly? PreferredEndTime { get; set; }

        // Session preferences
        public int? PreferredSessionDurationMinutes { get; set; } = 60;
        public string? SpecialRequirements { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
