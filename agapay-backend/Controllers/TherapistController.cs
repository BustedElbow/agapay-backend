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
    public class TherapistController : ControllerBase
    {
        private readonly agapayDbContext _context;
        private readonly ISupabaseStorageService _storageService;

        public TherapistController(agapayDbContext context, ISupabaseStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
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
