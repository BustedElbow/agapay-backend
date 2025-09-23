using agapay_backend.Data;
using agapay_backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Services
{
    public class AvailabilityService : IAvailabilityService
    {
        private readonly agapayDbContext _context;

        public AvailabilityService(agapayDbContext context)
        {
            _context = context;
        }

        public async Task<double> CalculateAvailabilityScore(int therapistId, int patientId)
        {
            var therapistAvailability = await _context.TherapistAvailabilities
                .Where(ta => ta.PhysicalTherapistId == therapistId && ta.IsAvailable)
                .ToListAsync();

            var patientPreferences = await _context.PatientPreferences
                .FirstOrDefaultAsync(pp => pp.PatientId == patientId);

            if (!therapistAvailability.Any())
                return 0.0;

            // If patient has no preferences, return default score
            if (patientPreferences?.PreferredDayOfWeek == null ||
                patientPreferences.PreferredStartTime == null ||
                patientPreferences.PreferredEndTime == null)
                return 0.5; // Default score when no patient preferences

            // Calculate overlapping time slots
            var matchingSlots = therapistAvailability.Where(ta =>
                ta.DayOfWeek == patientPreferences.PreferredDayOfWeek &&
                DoTimesOverlap(ta.StartTime, ta.EndTime,
                              patientPreferences.PreferredStartTime.Value,
                              patientPreferences.PreferredEndTime.Value))
                .ToList();

            if (!matchingSlots.Any())
                return 0.0;

            // Calculate total overlapping minutes
            var totalOverlapMinutes = matchingSlots.Sum(slot =>
                CalculateOverlapMinutes(slot.StartTime, slot.EndTime,
                                      patientPreferences.PreferredStartTime.Value,
                                      patientPreferences.PreferredEndTime.Value));

            // Calculate total preferred time in minutes
            var preferredDurationMinutes = (patientPreferences.PreferredEndTime.Value.ToTimeSpan() -
                                          patientPreferences.PreferredStartTime.Value.ToTimeSpan()).TotalMinutes;

            // Calculate score as ratio of overlap to preferred duration
            var score = Math.Min(totalOverlapMinutes / preferredDurationMinutes, 1.0);

            return score;
        }

        public async Task<List<TherapistAvailability>> GetTherapistAvailability(int therapistId)
        {
            return await _context.TherapistAvailabilities
                .Where(ta => ta.PhysicalTherapistId == therapistId)
                .OrderBy(ta => ta.DayOfWeek)
                .ThenBy(ta => ta.StartTime)
                .ToListAsync();
        }

        public async Task<bool> UpdateTherapistAvailability(int therapistId, List<TherapistAvailabilityDto> availabilities)
        {
            try
            {
                // Remove existing availability
                var existingAvailability = await _context.TherapistAvailabilities
                    .Where(ta => ta.PhysicalTherapistId == therapistId)
                    .ToListAsync();

                _context.TherapistAvailabilities.RemoveRange(existingAvailability);

                // Add new availability
                foreach (var availability in availabilities)
                {
                    _context.TherapistAvailabilities.Add(new TherapistAvailability
                    {
                        PhysicalTherapistId = therapistId,
                        DayOfWeek = availability.DayOfWeek,
                        StartTime = availability.StartTime,
                        EndTime = availability.EndTime,
                        IsAvailable = availability.IsAvailable,
                        Notes = availability.Notes
                    });
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<int>> GetAvailableTherapists(DayOfWeekEnum dayOfWeek, TimeOnly startTime, TimeOnly endTime)
        {
            return await _context.TherapistAvailabilities
                .Where(ta => ta.DayOfWeek == dayOfWeek &&
                           ta.IsAvailable &&
                           ta.StartTime <= startTime &&
                           ta.EndTime >= endTime)
                .Select(ta => ta.PhysicalTherapistId)
                .Distinct()
                .ToListAsync();
        }

        private bool DoTimesOverlap(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
        {
            return start1 < end2 && start2 < end1;
        }

        private double CalculateOverlapMinutes(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
        {
            var overlapStart = start1 > start2 ? start1 : start2;
            var overlapEnd = end1 < end2 ? end1 : end2;

            if (overlapStart >= overlapEnd)
                return 0;

            return (overlapEnd.ToTimeSpan() - overlapStart.ToTimeSpan()).TotalMinutes;
        }
    }
}