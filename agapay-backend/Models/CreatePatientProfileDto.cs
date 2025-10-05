namespace agapay_backend.Models
{
    public class CreatePatientProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? RelationshipToUser { get; set; }

        public string? Address { get; set; }
        public string? Barangay { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? Occupation { get; set; }
        public string? ActivityLevel { get; set; }
        public string? MedicalCondition { get; set; }
        public string? SurgicalHistory { get; set; }
        public string? MedicationBeingTaken { get; set; }
        public string? CurrentComplaints { get; set; }

        public bool? IsActive { get; set; }
    }
}
