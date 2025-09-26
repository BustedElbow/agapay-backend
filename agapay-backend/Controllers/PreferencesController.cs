using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Patient")]
    public class PreferencesController : ControllerBase
    {
        private readonly agapayDbContext _db;

        public PreferencesController(agapayDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyPreferences()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var patient = await _db.Patients
                .Include(p => p.Preferences)
                .FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId));
            if (patient is null) return NotFound("Patient not found");

            var pref = patient.Preferences;
            if (pref is null)
            {
                return Ok(new
                {
                    PreferredDayOfWeek = (DayOfWeekEnum?)null,
                    PreferredStartTime = (TimeOnly?)null,
                    PreferredEndTime = (TimeOnly?)null,
                    PreferredSessionDurationMinutes = (int?)null,
                    SpecialRequirements = (string?)null
                });
            }

            return Ok(new
            {
                pref.PreferredDayOfWeek,
                pref.PreferredStartTime,
                pref.PreferredEndTime,
                pref.PreferredSessionDurationMinutes,
                pref.SpecialRequirements
            });
        }

        [HttpPost("me")] // upsert
        public async Task<IActionResult> UpsertMyPreferences(PatientPreferencesDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var patient = await _db.Patients
                .Include(p => p.Preferences)
                .FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId));
            if (patient is null) return NotFound("Patient not found");

            if (patient.Preferences is null)
            {
                patient.Preferences = new PatientPreferences
                {
                    PatientId = patient.Id,
                    Patient = patient
                };
                _db.PatientPreferences.Add(patient.Preferences);
            }

            patient.Preferences.PreferredDayOfWeek = dto.PreferredDayOfWeek;
            patient.Preferences.PreferredStartTime = dto.PreferredStartTime;
            patient.Preferences.PreferredEndTime = dto.PreferredEndTime;
            patient.Preferences.PreferredSessionDurationMinutes = dto.PreferredSessionDurationMinutes;
            patient.Preferences.SpecialRequirements = dto.SpecialRequirements;
            patient.Preferences.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Preferences saved" });
        }
    }
}

