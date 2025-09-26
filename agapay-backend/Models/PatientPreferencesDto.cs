using agapay_backend.Entities;

namespace agapay_backend.Models
{
    public class PatientPreferencesDto
    {
        public DayOfWeekEnum? PreferredDayOfWeek { get; set; }
        public TimeOnly? PreferredStartTime { get; set; }
        public TimeOnly? PreferredEndTime { get; set; }
        public int? PreferredSessionDurationMinutes { get; set; }
        public string? SpecialRequirements { get; set; }
    }
}

