using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly agapayDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ISupabaseStorageService _storageService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(agapayDbContext context, UserManager<User> userManager, ISupabaseStorageService storageService, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _storageService = storageService;
            _logger = logger;
        }

        // Admin list: returns submissions for verification (no image URL or preview)
        [HttpGet("submissions")]
        public async Task<IActionResult> GetSubmissionsList()
        {
            // Return only the metadata for the admin list UI (no image)
            var submissions = await _context.PhysicalTherapists
                .Include(pt => pt.User)
                .Where(pt => pt.VerificationStatus == VerificationStatus.Pending || pt.VerificationStatus == VerificationStatus.Rejected)
                .OrderBy(pt => pt.SubmittedAt)
                .Select(pt => new
                {
                    pt.Id,
                    pt.UserId,
                    UserName = pt.User.FirstName + " " + pt.User.LastName,
                    pt.User.Email,
                    pt.LicenseNumber,
                    submittedAt = pt.SubmittedAt,
                    status = pt.VerificationStatus.ToString()
                })
                .ToListAsync();

            return Ok(submissions);
        }

        // Admin detail: returns full submission details including preview URL for the image
        [HttpGet("submissions/{therapistId}")]
        public async Task<IActionResult> GetSubmissionDetails(int therapistId)
        {
            var therapist = await _context.PhysicalTherapists
                .Include(pt => pt.User)
                .FirstOrDefaultAsync(pt => pt.Id == therapistId);

            if (therapist is null) return NotFound("Submission not found");

            string? previewUrl = null;
            if (!string.IsNullOrEmpty(therapist.LicenseImageUrl))
            {
                try
                {
                    // Request a short-lived signed URL for admin preview (60s)
                    previewUrl = await _storageService.GetSignedUrlAsync(therapist.LicenseImageUrl, 60);
                }
                catch
                {
                    previewUrl = null;
                }
            }

            return Ok(new
            {
                therapist.Id,
                therapist.UserId,
                UserName = therapist.User.FirstName + " " + therapist.User.LastName,
                therapist.User.Email,
                therapist.LicenseNumber,
                licenseImagePath = therapist.LicenseImageUrl,
                licensePreviewUrl = previewUrl,
                therapist.SubmittedAt,
                therapist.VerifiedAt,
                therapist.RejectionReason,
                status = therapist.VerificationStatus.ToString()
            });
        }

        [HttpPost("therapist-verifications/{therapistId}/verify")]
        public async Task<IActionResult> VerifyTherapist(int therapistId, TherapistVerificationDto verificationDto)
        {
            var therapist = await _context.PhysicalTherapists
                .Include(pt => pt.User)
                .FirstOrDefaultAsync(pt => pt.Id == therapistId);

            if (therapist is null)
            {
                return NotFound("Physical therapist not found.");
            }

            if (therapist.VerificationStatus is not VerificationStatus.Pending)
            {
                return BadRequest("This therapist is not pending verification");
            }

            // Validate rejection reason when rejecting (backend enforcement)
            if (!verificationDto.IsApproved && string.IsNullOrWhiteSpace(verificationDto.RejectionReason))
            {
                return BadRequest(new { error = "RejectionReason is required when IsApproved is false." });
            }

            // Approve flow
            if (verificationDto.IsApproved)
            {
                therapist.VerificationStatus = VerificationStatus.Verified;
                therapist.VerifiedAt = DateTime.UtcNow;
                therapist.RejectionReason = null;

                var user = therapist.User;
                var currentRoles = await _user_manager_GetRolesSafe(user);
                if (!currentRoles.Contains("PhysicalTherapist"))
                {
                    await _userManager.AddToRoleAsync(user, "PhysicalTherapist");
                }

                if (currentRoles.Contains("User"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "User");
                }

                // Attempt to delete the submitted image from Supabase (best-effort).
                bool imageDeleted = false;
                string? deleteError = null;
                if (!string.IsNullOrEmpty(therapist.LicenseImageUrl))
                {
                    try
                    {
                        // Always await and log error if deletion fails
                        await _storage_service_DeleteSafe(therapist.LicenseImageUrl);
                        therapist.LicenseImageUrl = null;
                        imageDeleted = true;
                    }
                    catch (Exception ex)
                    {
                        // Log error for admin review, but continue approval
                        _logger.LogError(ex, "Failed to delete license image from Supabase for therapistId {Id}", therapist.Id);
                        deleteError = ex.Message;
                    }
                }

                _context.PhysicalTherapists.Update(therapist);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Therapist Verified successfully",
                    status = therapist.VerificationStatus.ToString(),
                    licenseImageDeleted = imageDeleted,
                    licenseDeleteError = deleteError
                });
            }

            // Reject flow: delete the submitted image (if any), store rejection reason, set status
            try
            {
                if (!string.IsNullOrEmpty(therapist.LicenseImageUrl))
                {
                    // Attempt to delete stored image from Supabase
                    await _storage_service_DeleteSafe(therapist.LicenseImageUrl);

                    // Clear DB reference after successful delete
                    therapist.LicenseImageUrl = null;
                }
            }
            catch (Exception ex)
            {
                // If delete fails, return 500 so admin can retry; do not mark as rejected until image is removed.
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete license image from storage", detail = ex.Message });
            }

            therapist.VerificationStatus = VerificationStatus.Rejected;
            therapist.VerifiedAt = DateTime.UtcNow;
            therapist.RejectionReason = string.IsNullOrWhiteSpace(verificationDto.RejectionReason)
                ? "Rejected by admin"
                : verificationDto.RejectionReason;

            _context.PhysicalTherapists.Update(therapist);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Therapist verification rejected",
                status = therapist.VerificationStatus.ToString()
            });
        }

        // helper to avoid modifying original code style too much
        private async Task<IList<string>> _user_manager_GetRolesSafe(User user)
        {
            return user is null ? new List<string>() : await _userManager.GetRolesAsync(user);
        }

        [HttpDelete("therapist/{therapistId}/license")]
        public async Task<IActionResult> DeleteTherapistLicense(int therapistId)
        {
            var therapist = await _context.PhysicalTherapists
                .Include(pt => pt.User)
                .FirstOrDefaultAsync(pt => pt.Id == therapistId);

            if (therapist is null) return NotFound("Therapist not found");

            if (string.IsNullOrEmpty(therapist.LicenseImageUrl))
            {
                return NoContent();
            }

            try
            {
                await _storage_service_DeleteSafe(therapist.LicenseImageUrl);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete license image from storage", detail = ex.Message });
            }

            therapist.LicenseImageUrl = null;
            _context.PhysicalTherapists.Update(therapist);
            await _context.SaveChangesAsync();

            return Ok(new { message = "License image deleted and DB reference cleared" });
        }

        // wrappers to keep call sites clean (and avoid accidental rename issues)
        private async Task _storage_service_DeleteSafe(string path)
        {
            await _storageService.DeleteFileAsync(path);
        }

        private string? _storage_service_GetPublicUrlSafe(string path)
        {
            // delegate to storage service; keep exception handling at caller if needed
            return string.IsNullOrEmpty(path) ? null : _storageService.GetPublicUrl(path);
        }
    }
}
