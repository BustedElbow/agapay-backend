using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using agapay_backend.Models;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TherapistController : ControllerBase
    {
        private readonly agapayDbContext _context;
        private readonly ISupabaseStorageService _storageService;

        public TherapistController(agapayDbContext context, ISupabaseStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        // Returns a list of verified + onboarded therapists for card views
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ListTherapists([FromQuery] int top = 0)
        {
            var query = _context.PhysicalTherapists
                .AsNoTracking()
                .Where(t => t.IsOnboardingComplete && t.VerificationStatus == VerificationStatus.Verified)
                .Include(t => t.User)
                .Include(t => t.Specializations)
                .Include(t => t.ServiceAreas)
                .Select(t => new TherapistCardDto
                {
                    Id = t.Id,
                    Name = t.User != null ? (t.User.FirstName + " " + t.User.LastName) : t.LicenseNumber,
                    ProfilePictureUrl = t.ProfilePictureUrl, // will be converted to usable URL below
                    YearsOfExperience = t.YearsOfExperience,
                    AverageRating = t.AverageRating,
                    RatingCount = t.RatingCount,
                    FeePerSession = t.FeePerSession,
                    Specializations = t.Specializations.Select(s => s.Name),
                    ServiceAreas = t.ServiceAreas.Select(sa => sa.Name)
                });

            var list = top > 0 ? await query.Take(top).ToListAsync() : await query.ToListAsync();

            // Convert stored object paths into signed/public URLs (best-effort)
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.ProfilePictureUrl))
                {
                    var signed = await _storageService.GetSignedUrlAsync(item.ProfilePictureUrl!, 3600);
                    item.ProfilePictureUrl = signed ?? _storageService.GetPublicUrl(item.ProfilePictureUrl!);
                }
            }

            return Ok(list);
        }

        // Returns full therapist details by id (for detail page)
        [HttpGet("{therapistId:int}")]
        [Authorize]
        public async Task<IActionResult> GetTherapistDetails(int therapistId)
        {
            var t = await _context.PhysicalTherapists
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Specializations)
                .Include(x => x.ConditionsTreated)
                .Include(x => x.ServiceAreas)
                .FirstOrDefaultAsync(x => x.Id == therapistId && x.IsOnboardingComplete && x.VerificationStatus == VerificationStatus.Verified);

            if (t is null) return NotFound("Therapist not found");

            var dto = new TherapistDetailDto
            {
                Id = t.Id,
                Name = t.User != null ? ($"{t.User.FirstName} {t.User.LastName}") : t.LicenseNumber,
                ProfilePictureUrl = t.ProfilePictureUrl, // will convert below
                WorkPhoneNumber = t.WorkPhoneNumber,
                YearsOfExperience = t.YearsOfExperience,
                AverageRating = t.AverageRating,
                RatingCount = t.RatingCount,
                FeePerSession = t.FeePerSession,
                Specializations = t.Specializations.Select(s => s.Name).ToList(),
                ConditionsTreated = t.ConditionsTreated.Select(c => c.Name).ToList(),
                ServiceAreas = t.ServiceAreas.Select(sa => sa.Name).ToList()
            };

            if (!string.IsNullOrWhiteSpace(dto.ProfilePictureUrl))
            {
                var signed = await _storageService.GetSignedUrlAsync(dto.ProfilePictureUrl!, 3600);
                dto.ProfilePictureUrl = signed ?? _storageService.GetPublicUrl(dto.ProfilePictureUrl!);
            }

            return Ok(dto);
        }

        // Returns the authenticated therapist's own profile (basic details + status)
        [HttpGet("me")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> GetMyTherapistProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var t = await _context.PhysicalTherapists
                .Include(x => x.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == Guid.Parse(userId));

            if (t is null) return NotFound("Physical therapist not found");

            string? profileUrl = null;
            if (!string.IsNullOrWhiteSpace(t.ProfilePictureUrl))
            {
                var signed = await _storageService.GetSignedUrlAsync(t.ProfilePictureUrl!, 3600);
                profileUrl = signed ?? _storageService.GetPublicUrl(t.ProfilePictureUrl!);
            }

            return Ok(new
            {
                id = t.Id,
                name = t.User != null ? ($"{t.User.FirstName} {t.User.LastName}") : t.LicenseNumber,
                profilePictureUrl = profileUrl,
                yearsOfExperience = t.YearsOfExperience,
                averageRating = t.AverageRating,
                ratingCount = t.RatingCount,
                feePerSession = t.FeePerSession,
                isOnboardingComplete = t.IsOnboardingComplete,
                verificationStatus = t.VerificationStatus.ToString()
            });
        }

        // Returns the current authenticated therapist's usable profile picture URL (signed or public)
        [HttpGet("me/photo")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> GetMyProfilePhoto()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var therapist = await _context.PhysicalTherapists
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.UserId == Guid.Parse(userId));

            if (therapist is null) return NotFound("Physical therapist not found");

            if (string.IsNullOrWhiteSpace(therapist.ProfilePictureUrl))
                return Ok(new { profilePicture = (string?)null });

            var signed = await _storageService.GetSignedUrlAsync(therapist.ProfilePictureUrl, expiresInSeconds: 3600);
            var url = signed ?? _storageService.GetPublicUrl(therapist.ProfilePictureUrl);
            return Ok(new { profilePicture = url });
        }

        // Allows the authenticated therapist to set or update their fee
        [HttpPut("me/fee")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> UpdateMyFee(UpdateFeeDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var therapist = await _context.PhysicalTherapists
                .FirstOrDefaultAsync(pt => pt.UserId == Guid.Parse(userId));

            if (therapist is null) return NotFound("Physical therapist not found");

            therapist.FeePerSession = dto.FeePerSession;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Fee updated" });
        }

        // Returns another therapist's usable profile picture URL by therapist id (used by home/profiles list)
        [HttpGet("{therapistId:int}/photo")]
        [Authorize] // allow any authenticated user; change or open as needed
        public async Task<IActionResult> GetTherapistPhoto(int therapistId)
        {
            var therapist = await _context.PhysicalTherapists
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.Id == therapistId);

            if (therapist is null) return NotFound("Therapist not found");

            if (string.IsNullOrWhiteSpace(therapist.ProfilePictureUrl))
                return Ok(new { profilePicture = (string?)null });

            var signed = await _storageService.GetSignedUrlAsync(therapist.ProfilePictureUrl, expiresInSeconds: 3600);
            var url = signed ?? _storageService.GetPublicUrl(therapist.ProfilePictureUrl);
            return Ok(new { profilePicture = url });
        }
    }
}
