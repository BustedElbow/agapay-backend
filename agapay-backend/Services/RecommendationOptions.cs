namespace agapay_backend.Services
{
    public class RecommendationOptions
    {
        // Must be >= 0. If they don't sum to 1 we'll normalize at runtime.
        public double WeightAvailability { get; set; } = 0.22;
        public double WeightExperience { get; set; } = 0.17;
        public double WeightRating { get; set; } = 0.17;
        public double WeightBudget { get; set; } = 0.17;
        public double WeightSpecialization { get; set; } = 0.17;
        public double WeightServiceArea { get; set; } = 0.10;

        public int DefaultTop { get; set; } = 5;
        public int ExperienceBaselinePercent { get; set; } = 20; // optional helper, not required by code
    }
}
