using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AvailabilityController : ControllerBase
    {
        private readonly IAvailabilityService _availabilityService;

        public AvailabilityController(IAvailabilityService availabilityService)
        {
            _availabilityService = availabilityService;
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
    }
}