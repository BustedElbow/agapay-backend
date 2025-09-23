using agapay_backend.Models;
using agapay_backend.Entities;

namespace agapay_backend.Services
{
    public interface IRatingService
    {
        Task SubmitRatingAsync(SubmitRatingDto dto, Guid currentUserId);
        Task<(double normalizedScore, double rawBayes, int n, double avg, double globalAvg, int k)> ComputeNormalizedRatingAsync(int therapistId, int smoothingK = 5);
    }
}
