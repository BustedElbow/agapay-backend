using agapay_backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace agapay_backend.Services
{
    public class ExperienceNormalizationService : IExperienceNormalizationService
    {
        private readonly agapayDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private const string MaxYearsCacheKey = "Experience_MaxYears";

        public ExperienceNormalizationService(agapayDbContext db, IMemoryCache cache, IConfiguration config)
        {
            _db = db;
            _cache = cache;
            _config = config;
        }

        public async Task<int> GetMaxYearsAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue<int>(MaxYearsCacheKey, out var cached)) return cached;

            // fallback default max if DB empty or null
            var defaultMax = _config.GetValue<int?>("Recommendation:Experience:DefaultMaxYears") ?? 30;

            var maxFromDb = await _db.PhysicalTherapists
                .AsNoTracking()
                .MaxAsync(pt => (int?)pt.YearsOfExperience, ct);

            var maxYears = maxFromDb.HasValue && maxFromDb.Value > 0 ? maxFromDb.Value : defaultMax;

            // cache for a short time; configurable
            var cacheMinutes = _config.GetValue<int?>("Recommendation:Experience:MaxYearsCacheMinutes") ?? 5;
            _cache.Set(MaxYearsCacheKey, maxYears, TimeSpan.FromMinutes(cacheMinutes));

            return maxYears;
        }

        public async Task<double> GetNormalizedScoreAsync(int yearsOfExperience, double? baseline = null, CancellationToken ct = default)
        {
            // baseline a between 0 and 1; default 0.2
            var a = baseline ?? _config.GetValue<double?>("Recommendation:Experience:Baseline") ?? 0.2;
            if (a < 0) a = 0;
            if (a > 1) a = 1;

            var maxYears = await GetMaxYearsAsync(ct);

            // protect division by zero and negative years
            var x = Math.Max(0, yearsOfExperience);
            var denom = Math.Max(1, maxYears); // ensure >= 1

            var ratio = (double)x / denom;
            var raw = a + (1.0 - a) * ratio;
            var normalized = Math.Clamp(raw, 0.0, 1.0);

            return normalized;
        }
    }
}
