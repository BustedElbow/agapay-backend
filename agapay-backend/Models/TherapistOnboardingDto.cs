using System.Collections.Generic;

namespace agapay_backend.Models
{
    public class TherapistOnboardingDto
    {
        // Removed ProfilePictureUrl here to avoid duplicate representations of the same data.
        public int YearsOfExperience { get; set; }
        public List<int> SpecializationIds { get; set; } = new();
        public List<int> ConditionIds { get; set; } = new();
        public List<int> ServiceAreasIds { get; set; } = new();
    }
}
