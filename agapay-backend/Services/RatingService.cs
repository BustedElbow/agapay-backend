using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Services
{
    public class RatingService : IRatingService
    {
        private readonly agapayDbContext _context;

        public RatingService(agapayDbContext context)
        {
            _context = context;
        }

        // Enforce: patient must have a completed session with the therapist; one rating per session.
        public async Task SubmitRatingAsync(SubmitRatingDto dto, Guid currentUserId)
        {
            // find patient id for current user
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUserId);
            if (patient is null) throw new InvalidOperationException("Patient account not found.");

            // validate score
            if (dto.Score < 1 || dto.Score > 5) throw new ArgumentOutOfRangeException(nameof(dto.Score));

            // If session id provided, validate ownership and completion
            if (dto.SessionId.HasValue)
            {
                var session = await _context.TherapySessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == dto.SessionId.Value);

                if (session is null) throw new InvalidOperationException("Session not found.");
                if (session.PatientId != patient.Id) throw new UnauthorizedAccessException("You are not the owner of this session.");
                if (session.PhysicalTherapistId != dto.TherapistId) throw new InvalidOperationException("Session therapist mismatch.");
                if (session.Status != SessionStatus.Completed) throw new InvalidOperationException("Cannot rate until the session is completed.");

                // ensure no rating exists for this session
                var existing = await _context.TherapistRatings.FirstOrDefaultAsync(r => r.SessionId == session.Id);
                if (existing is not null) throw new InvalidOperationException("This session has already been rated.");
            }
            else
            {
                // If no session id, do a lighter validation: ensure patient has at least one completed session with therapist
                var anyCompleted = await _context.TherapySessions.AnyAsync(s =>
                    s.PatientId == patient.Id && s.PhysicalTherapistId == dto.TherapistId && s.Status == SessionStatus.Completed);
                if (!anyCompleted) throw new InvalidOperationException("No completed session found with this therapist.");
            }

            // Create rating
            var rating = new TherapistRating
            {
                PhysicalTherapistId = dto.TherapistId,
                PatientId = patient.Id,
                SessionId = dto.SessionId,
                Score = dto.Score,
                Comment = dto.Comment
            };

            await _context.TherapistRatings.AddAsync(rating);
            await _context.SaveChangesAsync();

            // Recompute aggregates safely from DB and update therapist
            var agg = await _context.TherapistRatings
                .Where(r => r.PhysicalTherapistId == dto.TherapistId)
                .GroupBy(r => r.PhysicalTherapistId)
                .Select(g => new { Count = g.Count(), Avg = g.Average(x => x.Score) })
                .FirstOrDefaultAsync();

            if (agg is not null)
            {
                var therapist = await _context.PhysicalTherapists.FirstOrDefaultAsync(t => t.Id == dto.TherapistId);
                if (therapist is not null)
                {
                    therapist.RatingCount = agg.Count;
                    therapist.AverageRating = agg.Avg;
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Compute Bayesian-normalized rating (rawBayes in 1..5) and normalized (0..1)
        public async Task<(double normalizedScore, double rawBayes, int n, double avg, double globalAvg, int k)> ComputeNormalizedRatingAsync(int therapistId, int smoothingK = 5)
        {
            var stat = await _context.TherapistRatings
                .Where(r => r.PhysicalTherapistId == therapistId)
                .GroupBy(r => r.PhysicalTherapistId)
                .Select(g => new { Count = g.Count(), Avg = (double?)g.Average(x => x.Score) })
                .FirstOrDefaultAsync();

            int n = stat?.Count ?? 0;
            double r = stat?.Avg ?? 0.0;

            var global = await _context.TherapistRatings.AverageAsync(r => (double?)r.Score) ?? 3.0; // default 3.0 if no ratings
            double m = global;
            int k = smoothingK;

            // raw Bayesian average scaled to 1..5
            double rawBayes;
            if (n == 0) rawBayes = m;
            else rawBayes = (n / (double)(n + k)) * r + (k / (double)(n + k)) * m;

            double normalized = Math.Clamp(rawBayes / 5.0, 0.0, 1.0);

            return (normalized, rawBayes, n, r, m, k);
        }
    }
}
