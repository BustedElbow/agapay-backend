using agapay_backend.Data;
using agapay_backend.Models;
using agapay_backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace agapay_backend.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly agapayDbContext _db;
        private readonly IAvailabilityService _availability;
        private readonly IExperienceNormalizationService _experience;
        private readonly IRatingService _rating;
        private readonly IBudgetNormalizationService _budget;
        private readonly RecommendationOptions _options;

        public RecommendationService(
            agapayDbContext db,
            IAvailabilityService availability,
            IExperienceNormalizationService experience,
            IRatingService rating,
            IBudgetNormalizationService budget,
            IOptions<RecommendationOptions> options)
        {
            _db = db;
            _availability = availability;
            _experience = experience;
            _rating = rating;
            _budget = budget;
            _options = options.Value;
        }

        public async Task<List<MatchDto>> GetRecommendationsAsync(
            int patientId,
            int top = 5,
            PatientPreferencesDto? patientPreferences = null,
            CancellationToken ct = default)
        {
            // Load patient + preferences for fallback context
            var patient = await _db.Patients
                .AsNoTracking()
                .Include(p => p.Preferences)
                .FirstOrDefaultAsync(p => p.Id == patientId, ct);

            if (patient is null) return new List<MatchDto>();

            var effectivePreferences = patientPreferences ?? (patient.Preferences is null
                ? null
                : new PatientPreferencesDto
                {
                    PreferredDayOfWeek = patient.Preferences.PreferredDayOfWeek,
                    PreferredStartTime = patient.Preferences.PreferredStartTime,
                    PreferredEndTime = patient.Preferences.PreferredEndTime,
                    SessionBudget = patient.Preferences.SessionBudget,
                    PreferredSpecialization = patient.Preferences.PreferredSpecialization,
                    DesiredService = patient.Preferences.DesiredService,
                    PreferredBarangay = patient.Preferences.PreferredBarangay,
                    PreferredTherapistGender = patient.Preferences.PreferredTherapistGender
                });

            var effectiveBudget = effectivePreferences?.SessionBudget;

            // Build weight vector and normalize
            var w = new Dictionary<string, double>
            {
                ["availability"] = _options.WeightAvailability,
                ["experience"] = _options.WeightExperience,
                ["rating"] = _options.WeightRating,
                ["budget"] = _options.WeightBudget,
                ["specialization"] = _options.WeightSpecialization,
                ["desiredService"] = _options.WeightDesiredService
            };
            var totalW = w.Values.Sum();
            if (totalW <= 0) totalW = 1.0;
            var normalizedW = w.ToDictionary(kv => kv.Key, kv => kv.Value / totalW);

            // Pre-filter therapists: verified & onboarding complete (tunable)
            var candidates = await _db.PhysicalTherapists
                .AsNoTracking()
                .Where(t => t.IsOnboardingComplete && t.VerificationStatus == VerificationStatus.Verified)
                .Include(t => t.Specializations)
                .Include(t => t.ConditionsTreated)
                .Include(t => t.ServiceAreas)
                .ToListAsync(ct);

            var results = new List<MatchDto>(candidates.Count);

            foreach (var t in candidates)
            {
                // Availability
                double availabilityScore = 0.0;
                try
                {
                    // IAvailabilityService.CalculateAvailabilityScore returns [0..1]
                    availabilityScore = await _availability.CalculateAvailabilityScore(t.Id, patientId);
                }
                catch
                {
                    availabilityScore = 0.0;
                }

                // Experience
                double experienceScore = await _experience.GetNormalizedScoreAsync(t.YearsOfExperience);

                // Rating (use rating service Bayesian normalized score)
                double ratingScore = 0.0;
                try
                {
                    var ratingResult = await _rating.ComputeNormalizedRatingAsync(t.Id);
                    ratingScore = ratingResult.normalizedScore;
                }
                catch
                {
                    ratingScore = 0.0;
                }

                // Budget
                double budgetScore = _budget.ComputeBudgetScore(t.FeePerSession, effectiveBudget);

                // Specialization matching: strictly binary
                double specializationScore = 0.5; // neutral if no preference provided
                var specPref = effectivePreferences?.PreferredSpecialization;
                if (!string.IsNullOrWhiteSpace(specPref))
                {
                    specializationScore = t.Specializations.Any(s => string.Equals(s.Name, specPref, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;
                }

                // Service area: prefer barangay preference, then stored patient location
                double desiredServiceScore = 0.5;
                var desiredService = effectivePreferences?.DesiredService;
                if (!string.IsNullOrWhiteSpace(desiredService))
                {
                    bool matchesConditionList = t.ConditionsTreated.Any(c =>
                    !string.IsNullOrWhiteSpace(c.Name) &&
                    c.Name.Contains(desiredService, StringComparison.OrdinalIgnoreCase));

                    bool matchesOther = !string.IsNullOrWhiteSpace(t.OtherConditionsTreated) && t.OtherConditionsTreated.Contains(desiredService, StringComparison.OrdinalIgnoreCase);

                    desiredServiceScore = (matchesConditionList || matchesOther) ? 1.0 : 0.0;
                }

                // Weighted sum
                var breakdown = new Dictionary<string, double>
                {
                    ["availability"] = availabilityScore,
                    ["experience"] = experienceScore,
                    ["rating"] = ratingScore,
                    ["budget"] = budgetScore,
                    ["specialization"] = specializationScore,
                    ["desiredService"] = desiredServiceScore
                };

                double score = 0.0;
                foreach (var kv in breakdown)
                {
                    score += normalizedW[kv.Key] * kv.Value;
                }

                results.Add(new MatchDto
                {
                    TherapistId = t.Id,
                    TherapistName = t.User != null ? $"{t.User.FirstName} {t.User.LastName}" : t.LicenseNumber,
                    ProfilePictureUrl = t.ProfilePictureUrl,
                    MatchScore = Math.Clamp(score, 0.0, 1.0),
                    Breakdown = breakdown,
                    YearsOfExperience = t.YearsOfExperience,
                    AverageRating = t.AverageRating,
                    RatingCount = t.RatingCount,
                    FeePerSession = t.FeePerSession,
                    Specializations = t.Specializations.Select(s => s.Name).ToList(),
                    ServiceAreas = t.ServiceAreas.Select(sa => sa.Name).ToList()
                });
            }

            // Return top-N
            return results
                .OrderByDescending(r => r.MatchScore)
                .ThenByDescending(r => r.Breakdown["rating"])
                .Take(Math.Max(1, top))
                .ToList();
        }
    }
}
