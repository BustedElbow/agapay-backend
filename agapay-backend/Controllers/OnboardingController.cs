using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly agapayDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ISupabaseStorageService _storageService;

        public OnboardingController(agapayDbContext context, UserManager<User> userManager, ISupabaseStorageService storageService)
        {
            _context = context;
            _userManager = userManager;
            _storageService = storageService;
        }

        // Accept multipart/form-data - license image uploaded from phone
        [HttpPost("therapist/submit-license")]
        [Authorize]
        public async Task<IActionResult> SubmitTherapistLicense([FromForm] TherapistLicenseSubmissionDto licenseDto, IFormFile? licenseImage)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var userGuid = Guid.Parse(userId);

            string? uploadedObjectPath = null;

            // If client provided an image file, upload to Supabase and get the object path (store that)
            if (licenseImage != null)
            {
                try
                {
                    // Upload into "licenses" folder (bucket folder). Returns object path like "licenses/{guid}.jpg".
                    uploadedObjectPath = await _storageService.UploadFileAsync(licenseImage, "licenses");
                }
                catch (Exception ex)
                {
                    // Bubble up a useful error to the client
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to upload license image", detail = ex.Message });
                }
            }

            // Try to find existing therapist record for this user
            var therapist = await _context.PhysicalTherapists
                .FirstOrDefaultAsync(pt => pt.UserId == userGuid);

            // If a record exists and verification is pending, do not allow another submission
            if (therapist is not null && therapist.VerificationStatus == VerificationStatus.Pending)
            {
                // Return 409 Conflict with the stored object path (so frontend can show pending screen)
                return Conflict(new
                {
                    message = "Verification pending. You cannot submit another license while your verification is under review.",
                    licenseImagePath = therapist.LicenseImageUrl,
                    submittedAt = therapist.SubmittedAt
                });
            }

            // If no record exists, create one (only once) and set status to Pending.
            if (therapist is null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user is null) return NotFound("User not found");

                // Safety check to avoid duplicates (concurrent requests)
                if (await _context.PhysicalTherapists.AnyAsync(pt => pt.UserId == userGuid))
                {
                    therapist = await _context.PhysicalTherapists.FirstOrDefaultAsync(pt => pt.UserId == userGuid);
                }
                else
                {
                    therapist = new PhysicalTherapist
                    {
                        UserId = userGuid,
                        User = user,
                        LicenseNumber = licenseDto.LicenseNumber ?? string.Empty,
                        LicenseImageUrl = uploadedObjectPath, // store object path
                        SubmittedAt = DateTime.UtcNow,
                        VerificationStatus = VerificationStatus.Pending,
                        YearsOfExperience = 0,
                        IsOnboardingComplete = false
                    };
                    _context.PhysicalTherapists.Add(therapist);
                }
            }
            else
            {
                // If therapist already verified, don't allow resubmission
                if (therapist.VerificationStatus == VerificationStatus.Verified)
                {
                    return BadRequest("Physical therapist already verified");
                }

                // If therapist was rejected, allow resubmission — update license info and set to pending
                therapist.LicenseNumber = licenseDto.LicenseNumber ?? therapist.LicenseNumber;

                // If a new file was uploaded, update the stored object path; otherwise keep existing
                if (!string.IsNullOrEmpty(uploadedObjectPath))
                {
                    // If there was a previous license image path, attempt to delete it (best-effort)
                    if (!string.IsNullOrEmpty(therapist.LicenseImageUrl))
                    {
                        try
                        {
                            await _storageService.DeleteFileAsync(therapist.LicenseImageUrl);
                        }
                        catch
                        {
                            // swallow; not fatal for resubmission
                        }
                    }

                    therapist.LicenseImageUrl = uploadedObjectPath;
                }

                therapist.SubmittedAt = DateTime.UtcNow;
                therapist.VerificationStatus = VerificationStatus.Pending;
            }

            await _context.SaveChangesAsync();

            // Do not return the full public URL here; return the stored path and let the client request preview via a protected admin route or let UI construct preview if appropriate.
            return Ok(new
            {
                message = "License information submitted successfully",
                status = "Pending",
                licenseImagePath = therapist.LicenseImageUrl,
                submittedAt = therapist.SubmittedAt
            });
        }

        [HttpGet("therapist/verification-status")]
        [Authorize]
        public async Task<IActionResult> GetVerificationStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var therapist = await _context.PhysicalTherapists
                .FirstOrDefaultAsync(pt => pt.UserId == Guid.Parse(userId));

            if (therapist is null)
            {
                // No submission yet — client can show role selection or submission screen
                return Ok(new
                {
                    status = (string?)null,
                    submittedAt = (DateTime?)null,
                    verifiedAt = (DateTime?)null,
                    rejectionReason = (string?)null,
                    licenseImagePath = (string?)null,
                    // canSubmit true because there is no pending/verified submission
                    canSubmit = true
                });
            }

            // canSubmit: allow submission only if previously rejected (user may re-submit)
            bool canSubmit = therapist.VerificationStatus == VerificationStatus.Rejected;

            return Ok(new
            {
                status = therapist.VerificationStatus.ToString(),
                submittedAt = therapist.SubmittedAt,
                verifiedAt = therapist.VerifiedAt,
                rejectionReason = therapist.RejectionReason,
                // return stored object path only (no public URL)
                licenseImagePath = therapist.LicenseImageUrl,
                canSubmit
            });
        }

        [HttpGet("patient/user-info")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetUserInfoForOnboarding()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return NotFound("User not found");

            return Ok(new
            {
                firstName = user.FirstName,
                lastName = user.LastName,
                dateOfBirth = user.DateOfBirth,
                email = user.Email
            });
        }

        // Moved patient profiles list to PatientProfilesController at /api/patient/profiles

        [HttpGet("patient/status")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetPatientOnboardingStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var guid = Guid.Parse(userId);

            var hasCompleted = await _context.Patients
                .AnyAsync(p => p.UserId == guid && p.IsOnboardingComplete);

            var selfPatient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == guid && p.RelationshipToUser == "Self");

            var patientsCount = await _context.Patients
                .CountAsync(p => p.UserId == guid && p.IsActive);

            return Ok(new
            {
                isPatientOnboardingComplete = hasCompleted,
                hasSelfProfile = selfPatient != null,
                selfPatientId = selfPatient?.Id,
                patientsCount
            });
        }

        [HttpPost("patient")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CompletePatientOnboarding(PatientOnboardingDto onboardingDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            // Guard: If any patient profile for this account has already completed onboarding,
            // do not allow running the patient onboarding flow again.
            var alreadyCompleted = await _context.Patients
                .AnyAsync(p => p.UserId == Guid.Parse(userId) && p.IsOnboardingComplete);

            if (alreadyCompleted)
            {
                var profiles = await _context.Patients
                    .Where(p => p.UserId == Guid.Parse(userId) && p.IsActive)
                    .Select(p => new
                    {
                        id = p.Id,
                        firstName = p.FirstName,
                        lastName = p.LastName,
                        relationshipToUser = p.RelationshipToUser,
                        isOnboardingComplete = p.IsOnboardingComplete
                    })
                    .ToListAsync();

                return Conflict(new
                {
                    message = "Patient onboarding already completed for this account. Add more profiles from the home screen.",
                    profiles
                });
            }

            // Validate onboarding type
            if (onboardingDto.OnboardingType != "ForMyself" && onboardingDto.OnboardingType != "ForSomeoneElse")
            {
                return BadRequest("Invalid onboarding type. Must be 'ForMyself' or 'ForSomeoneElse'");
            }

            Patient patient;
            bool isNewPatient = false;

            if (onboardingDto.OnboardingType == "ForMyself")
            {
                // Check if user already has a "Self" patient record
                patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId) && p.RelationshipToUser == "Self");

                if (patient == null)
                {
                    // Create a new "Self" patient record if it doesn't exist
                    patient = new Patient
                    {
                        UserId = Guid.Parse(userId),
                        User = user,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        DateOfBirth = user.DateOfBirth,
                        RelationshipToUser = "Self",
                        IsActive = true,
                        IsOnboardingComplete = false
                    };
                    isNewPatient = true;
                }
                else
                {
                    // Update existing self record
                    patient.FirstName = user.FirstName;
                    patient.LastName = user.LastName;
                    patient.DateOfBirth = user.DateOfBirth;
                }
            }
            else // ForSomeoneElse
            {
                // Validate required fields for "ForSomeoneElse"
                if (string.IsNullOrWhiteSpace(onboardingDto.FirstName) ||
                    string.IsNullOrWhiteSpace(onboardingDto.LastName) ||
                    onboardingDto.DateOfBirth == null ||
                    string.IsNullOrWhiteSpace(onboardingDto.RelationshipToUser))
                {
                    return BadRequest("FirstName, LastName, DateOfBirth, and RelationshipToUser are required for 'ForSomeoneElse' onboarding");
                }

                // Create new patient record for someone else
                patient = new Patient
                {
                    UserId = Guid.Parse(userId),
                    User = user,
                    FirstName = onboardingDto.FirstName,
                    LastName = onboardingDto.LastName,
                    DateOfBirth = onboardingDto.DateOfBirth.Value,
                    RelationshipToUser = onboardingDto.RelationshipToUser,
                    IsActive = true,
                    IsOnboardingComplete = false
                };
                isNewPatient = true;
            }

            // Update common onboarding fields for both scenarios
            patient.Address = onboardingDto.Address;
            patient.Latitude = onboardingDto.Latitude;
            patient.Longitude = onboardingDto.Longitude;
            patient.LocationDisplayName = onboardingDto.LocationDisplayName;
            patient.ActivityLevel = onboardingDto.ActivityLevel;
            patient.MedicalCondition = onboardingDto.MedicalCondition;
            patient.SurgicalHistory = onboardingDto.SurgicalHistory;
            patient.MedicationBeingTaken = onboardingDto.MedicationBeingTaken;
            patient.CurrentComplaints = onboardingDto.CurrentComplaints;
            patient.Occupation = onboardingDto.Occupation;
            patient.IsOnboardingComplete = true;

            // Add or update the patient record
            if (isNewPatient)
            {
                _context.Patients.Add(patient);
            }
            else
            {
                _context.Patients.Update(patient);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Patient onboarding completed successfully.",
                patientId = patient.Id,
                onboardingType = onboardingDto.OnboardingType,
                relationshipToUser = patient.RelationshipToUser
            });
        }

        [HttpGet("patient/check-self-profile")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CheckSelfProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var selfPatient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == Guid.Parse(userId) && p.RelationshipToUser == "Self");

            return Ok(new
            {
                hasSelfProfile = selfPatient != null,
                isSelfOnboardingComplete = selfPatient?.IsOnboardingComplete ?? false,
                selfPatientId = selfPatient?.Id
            });
        }

        [HttpPost("therapist")]
        [Authorize(Roles = "PhysicalTherapist")]
        public async Task<IActionResult> CompleteTherapistOnboarding([FromForm] TherapistOnboardingDto onboardingDto, IFormFile? profilePicture)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var therapist = await _context.PhysicalTherapists
                .Include(pt => pt.Specializations)
                .Include(pt => pt.ConditionsTreated)
                .Include(pt => pt.ServiceAreas)
                .FirstOrDefaultAsync(pt => pt.UserId == Guid.Parse(userId));

            if (therapist is null) {
                return NotFound("Physical therapist not found.");
            }

            if (therapist.VerificationStatus != Entities.VerificationStatus.Verified)
            {
                return BadRequest("Cannot complete onboarding until verification is verified");
            }

            // If a profile picture file was uploaded, upload it to Supabase and set the object path.
            if (profilePicture != null)
            {
                string? uploadedProfilePath = null;
                try
                {
                    uploadedProfilePath = await _storageService.UploadFileAsync(profilePicture, $"profiles/{userId}");
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to upload profile picture", detail = ex.Message });
                }

                if (!string.IsNullOrEmpty(uploadedProfilePath))
                {
                    // If there was an existing profile picture stored, attempt to delete it (best-effort).
                    if (!string.IsNullOrEmpty(therapist.ProfilePictureUrl))
                    {
                        try
                        {
                            await _storageService.DeleteFileAsync(therapist.ProfilePictureUrl);
                        }
                        catch
                        {
                            // swallow; not critical
                        }
                    }

                    therapist.ProfilePictureUrl = uploadedProfilePath;
                }
            }

            therapist.YearsOfExperience = onboardingDto.YearsOfExperience;
            therapist.OtherConditionsTreated = onboardingDto.OtherConditions;
            if (onboardingDto.FeePerSession.HasValue)
            {
                therapist.FeePerSession = onboardingDto.FeePerSession;
            }

            therapist.Specializations.Clear();
            therapist.ConditionsTreated.Clear();
            therapist.ServiceAreas.Clear();

            if (onboardingDto.SpecializationIds.Any())
            {
                var specializations = await _context.Specializations
                    .Where(s => onboardingDto.SpecializationIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var specialization in specializations)
                {
                    therapist.Specializations.Add(specialization);
                }
            }

            if (onboardingDto.ConditionIds.Any())
            {
                var conditions = await _context.ConditionsTreated
                    .Where(c => onboardingDto.ConditionIds.Contains(c.Id))
                    .ToListAsync();
                
                foreach (var condition in conditions)
                {
                    therapist.ConditionsTreated.Add(condition);
                }
            }
            
            if (onboardingDto.ServiceAreasIds.Any())
            {
                var serviceAreas = await _context.ServiceAreas
                    .Where(sa => onboardingDto.ServiceAreasIds.Contains(sa.Id))
                    .ToListAsync();

                foreach (var serviceArea in serviceAreas)
                {
                    therapist.ServiceAreas.Add(serviceArea);
                }
            }

            therapist.IsOnboardingComplete = true;

            _context.PhysicalTherapists.Update(therapist);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Theraspist Onboarding completed successfully" });
        }

        [HttpGet("specializations")]
        public async Task<IActionResult> GetSpecializations()
        {
            var specializations = await _context.Specializations
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();

            return Ok(specializations);
        }

        [HttpGet("conditions")]
        public async Task<IActionResult> GetConditions()
        {
            var conditions = await _context.ConditionsTreated
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            return Ok(conditions);
        }

        [HttpGet("conditions-grouped")]
        public async Task<IActionResult> GetConditionsGrouped()
        {
            var conditions = await _context.ConditionsTreated
                .Select(c => new { c.Id, c.Name, c.Category })
                .ToListAsync();

            var grouped = conditions
                .GroupBy(c => c.Category)
                .Select(g => new
                {
                    category = g.Key.ToString(),
                    items = g.Select(x => new { x.Id, x.Name })
                });

            return Ok(grouped);
        }

        [HttpGet("service-areas")]
        public async Task<IActionResult> GetServiceAreas()
        {
            var serviceAreas = await _context.ServiceAreas
                .Select(sa => new { sa.Id, sa.Name })
                .ToListAsync();

            return Ok(serviceAreas);
        }
    }
}
