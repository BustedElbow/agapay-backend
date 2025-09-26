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
    public class SessionsController : ControllerBase
    {
        private readonly agapayDbContext _db;

        public SessionsController(agapayDbContext db)
        {
            _db = db;
        }

        // Patients create sessions with a therapist
        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreateSession(CreateSessionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId));
            if (patient is null) return NotFound("Patient not found");

            // Validate therapist exists
            var therapist = await _db.PhysicalTherapists.AsNoTracking().FirstOrDefaultAsync(t => t.Id == dto.TherapistId);
            if (therapist is null) return NotFound("Therapist not found");

            // Basic validation
            if (dto.EndAt <= dto.StartAt)
                return BadRequest(new { message = "EndAt must be after StartAt" });

            if (dto.StartAt < DateTime.UtcNow.AddMinutes(-1))
                return BadRequest(new { message = "StartAt must be in the future (UTC)" });

            var duration = (int)Math.Max(0, (dto.EndAt - dto.StartAt).TotalMinutes);

            // Ensure requested time falls within an available block for the therapist for that day
            var day = (DayOfWeekEnum)((int)dto.StartAt.DayOfWeek);
            var startT = TimeOnly.FromDateTime(dto.StartAt);
            var endT = TimeOnly.FromDateTime(dto.EndAt);

            var hasAvailability = await _db.TherapistAvailabilities
                .AsNoTracking()
                .AnyAsync(a => a.PhysicalTherapistId == dto.TherapistId
                            && a.IsAvailable
                            && a.DayOfWeek == day
                            && a.StartTime <= startT
                            && a.EndTime >= endT);

            if (!hasAvailability)
            {
                return BadRequest(new { message = "Requested time is outside therapist availability" });
            }

            // Check conflicts: any overlapping scheduled session for this therapist
            var overlapsTherapist = await _db.TherapySessions
                .AsNoTracking()
                .AnyAsync(s => s.PhysicalTherapistId == dto.TherapistId
                            && s.Status == SessionStatus.Scheduled
                            && s.StartAt < dto.EndAt && dto.StartAt < s.EndAt);

            if (overlapsTherapist)
            {
                return Conflict(new { message = "Requested time is no longer available" });
            }

            // Optional: prevent patient double-booking overlapping sessions
            var overlapsPatient = await _db.TherapySessions
                .AsNoTracking()
                .AnyAsync(s => s.PatientId == patient.Id
                            && s.Status == SessionStatus.Scheduled
                            && s.StartAt < dto.EndAt && dto.StartAt < s.EndAt);

            if (overlapsPatient)
            {
                return Conflict(new { message = "You already have a session overlapping this time" });
            }

            var session = new TherapySession
            {
                PatientId = patient.Id,
                PhysicalTherapistId = dto.TherapistId,
                LocationAddress = dto.LocationAddress,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                DurationMinutes = duration,
                DoctorReferralImageUrl = dto.DoctorReferralImageUrl,
                TotalFee = dto.TotalFee ?? 0,
                PatientFee = dto.PatientFee ?? 0,
                Status = SessionStatus.Scheduled
            };

            _db.TherapySessions.Add(session);
            await _db.SaveChangesAsync();
            return Ok(new { session.Id });
        }

        // Therapist marks session as completed (required for rating)
        [HttpPut("{sessionId:int}/complete")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> Complete(int sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var me = await _db.PhysicalTherapists.FirstOrDefaultAsync(t => t.UserId == Guid.Parse(userId));
            if (me is null) return Forbid();

            var session = await _db.TherapySessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return NotFound("Session not found");
            if (session.PhysicalTherapistId != me.Id) return Forbid();

            session.Status = SessionStatus.Completed;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Session marked completed" });
        }

        // Either party can cancel a session
        [HttpPut("{sessionId:int}/cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel(int sessionId, CancelSessionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var session = await _db.TherapySessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return NotFound("Session not found");

            var therapist = await _db.PhysicalTherapists.FirstOrDefaultAsync(t => t.UserId == Guid.Parse(userId));
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId));

            var isTherapist = therapist is not null && therapist.Id == session.PhysicalTherapistId;
            var isPatient = patient is not null && patient.Id == session.PatientId;
            if (!isTherapist && !isPatient) return Forbid();

            session.Status = SessionStatus.Cancelled;
            session.CancellationReason = dto.Reason;
            session.CancelledBy = isTherapist ? CancellationInitiator.Therapist : CancellationInitiator.Patient;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Session cancelled" });
        }
    }
}
