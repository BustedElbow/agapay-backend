using agapay_backend.Entities;

namespace agapay_backend.Models
{
    public class PatientPreferencesDto
    {
        public DayOfWeekEnum? PreferredDayOfWeek { get; set; }
        public TimeOnly? PreferredStartTime { get; set; }
        public TimeOnly? PreferredEndTime { get; set; }
        public decimal? SessionBudget { get; set; }
        public string? PreferredSpecialization { get; set; }
        public string? DesiredService { get; set; }
        public string? PreferredBarangay { get; set; }
        public string? PreferredTherapistGender { get; set; }
    }
}
