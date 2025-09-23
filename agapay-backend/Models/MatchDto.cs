namespace agapay_backend.Models
{
    public class MatchDto
    {
        public int TherapistId { get; set; }
        public string TherapistName { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }

        // Final weighted score [0..1]
        public double MatchScore { get; set; }

        // Breakdown for explainability
        public Dictionary<string, double> Breakdown { get; set; } = new();

        // Helpful details for UI
        public int YearsOfExperience { get; set; }
        public double? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public decimal? FeePerSession { get; set; }
        public IEnumerable<string> Specializations { get; set; } = Array.Empty<string>();
        public IEnumerable<string> ServiceAreas { get; set; } = Array.Empty<string>();
    }
}
