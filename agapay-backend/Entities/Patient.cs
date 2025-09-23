﻿using System.ComponentModel.DataAnnotations.Schema;

namespace agapay_backend.Entities
{
    public class Patient
    {
        public int Id { get; set; }
        [ForeignKey("UserId")]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        // Patient-specific details
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public required string RelationshipToUser { get; set; }

        public string? Address { get; set; }
        public bool IsActive { get; set; }

        // Location fields for map functionality
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? LocationDisplayName { get; set; }

        // Fields from Patient Onboarding
        public string? Occupation { get; set; }
        public string? ActivityLevel { get; set; }
        public string? MedicalCondition { get; set; }
        public string? SurgicalHistory { get; set; }
        public string? MedicationBeingTaken { get; set; }
        public string? CurrentComplaints { get; set; }

        // Relationship to preferences
        public PatientPreferences? Preferences { get; set; }

        public bool IsOnboardingComplete { get; set; }
    }
}
