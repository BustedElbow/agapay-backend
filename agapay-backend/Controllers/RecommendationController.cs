using agapay_backend.Data;
using agapay_backend.Models;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecommendationController : ControllerBase
    {
        private readonly IRecommendationService _recService;

        public RecommendationController(IRecommendationService recService)
        {
            _recService = recService;
        }

        [HttpGet("me")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetMyRecommendations([FromQuery] int top = 5)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            // Resolve patientId from user id (cheap DB lookup)
            // For small systems, you can include a PatientService; for brevity we'll query here.
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agapayDbContext>();
            var patient = await db.Patients
                .Include(p => p.Preferences)
                .FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId));
            if (patient is null) return NotFound("Patient not found");

            PatientPreferencesDto? preferencesDto = patient.Preferences is null
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
                };

            var recommendations = await _recService.GetRecommendationsAsync(patient.Id, top, preferencesDto);
            return Ok(recommendations);
        }
    }
}
