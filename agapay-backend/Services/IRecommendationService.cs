using agapay_backend.Models;

namespace agapay_backend.Services
{
    public interface IRecommendationService
    {
        Task<List<MatchDto>> GetRecommendationsAsync(int patientId, int top = 5, decimal? patientBudget = null, CancellationToken ct = default);
    }
}
