using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AvailabilityController : ControllerBase
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly agapayDbContext _db;

        public AvailabilityController(IAvailabilityService availabilityService, agapayDbContext db)
        {
            _availabilityService = availabilityService;
            _db = db;
        }

        [HttpGet("therapist/{therapistId}")]
        public async Task<IActionResult> GetTherapistAvailability(int therapistId)
        {
            var availability = await _availabilityService.GetTherapistAvailability(therapistId);
            return Ok(availability);
        }

        [HttpPost("therapist/{therapistId}")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> UpdateTherapistAvailability(int therapistId, List<TherapistAvailabilityDto> availabilities)
        {
            // Ensure the authenticated therapist matches the route therapistId
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var me = await _db.PhysicalTherapists.FirstOrDefaultAsync(t => t.UserId == Guid.Parse(userId));
            if (me is null) return Forbid();
            if (me.Id != therapistId) return Forbid();

            var success = await _availabilityService.UpdateTherapistAvailability(therapistId, availabilities);

            if (success)
                return Ok(new { message = "Availability updated successfully" });

            return BadRequest(new { message = "Failed to update availability" });
        }

        [HttpGet("score/{therapistId}/{patientId}")]
        public async Task<IActionResult> GetAvailabilityScore(int therapistId, int patientId)
        {
            var score = await _availabilityService.CalculateAvailabilityScore(therapistId, patientId);
            return Ok(new { score });
        }

        // Returns booked session intervals for a therapist within a range (sanitized: no details)
        [HttpGet("therapist/{therapistId}/booked")]
        public async Task<IActionResult> GetBookedIntervals(int therapistId, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (to <= from)
            {
                return BadRequest(new { message = "Query param 'to' must be after 'from'" });
            }

            var sessions = await _db.TherapySessions
                .AsNoTracking()
                .Where(s => s.PhysicalTherapistId == therapistId
                         && s.Status == SessionStatus.Scheduled
                         && s.StartAt < to && from < s.EndAt)
                .Select(s => new Models.BookedSlotDto
                {
                    StartAt = s.StartAt,
                    EndAt = s.EndAt
                })
                .OrderBy(s => s.StartAt)
                .ToListAsync();

            return Ok(sessions);
        }
    }
}
