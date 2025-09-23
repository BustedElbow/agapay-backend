namespace agapay_backend.Services
{
    public interface IExperienceNormalizationService
    {
        /// <summary>
        /// Compute normalized experience score in [0,1] using soft-weighted formula:
        /// score = a + (1-a) * (years / maxYears)
        /// </summary>
        Task<double> GetNormalizedScoreAsync(int yearsOfExperience, double? baseline = null, CancellationToken ct = default);

        /// <summary>
        /// Helper to get cached max years (for debugging/testing).
        /// </summary>
        Task<int> GetMaxYearsAsync(CancellationToken ct = default);
    }
}
