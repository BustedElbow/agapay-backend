using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/patient/profiles")]
    [ApiController]
    [Authorize(Roles = "Patient")]
    public class PatientProfilesController : ControllerBase
    {
        private readonly agapayDbContext _db;

        public PatientProfilesController(agapayDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> ListProfiles()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();
            var guid = Guid.Parse(userId);

            var patients = await _db.Patients
                .Where(p => p.UserId == guid && p.IsActive)
                .Select(p => new
                {
                    id = p.Id,
                    firstName = p.FirstName,
                    lastName = p.LastName,
                    dateOfBirth = p.DateOfBirth,
                    relationshipToUser = p.RelationshipToUser,
                    isOnboardingComplete = p.IsOnboardingComplete,
                    address = p.Address,
                    latitude = p.Latitude,
                    longitude = p.Longitude,
                    locationDisplayName = p.LocationDisplayName,
                    occupation = p.Occupation,
                    activityLevel = p.ActivityLevel,
                    medicalCondition = p.MedicalCondition,
                    surgicalHistory = p.SurgicalHistory,
                    medicationBeingTaken = p.MedicationBeingTaken,
                    currentComplaints = p.CurrentComplaints
                })
                .ToListAsync();

            return Ok(patients);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile(CreatePatientProfileDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();
            var guid = Guid.Parse(userId);

            if (string.IsNullOrWhiteSpace(dto.FirstName) ||
                string.IsNullOrWhiteSpace(dto.LastName) ||
                dto.DateOfBirth is null ||
                string.IsNullOrWhiteSpace(dto.RelationshipToUser))
            {
                return BadRequest("FirstName, LastName, DateOfBirth, and RelationshipToUser are required");
            }

            var existing = await _db.Patients.FirstOrDefaultAsync(p =>
                p.UserId == guid &&
                p.FirstName == dto.FirstName &&
                p.LastName == dto.LastName &&
                p.DateOfBirth == dto.DateOfBirth &&
                p.RelationshipToUser == dto.RelationshipToUser &&
                p.IsActive);

            if (existing is not null)
            {
                return Conflict(new { message = "Profile already exists", existingProfileId = existing.Id });
            }

            var patient = new Patient
            {
                UserId = guid,
                User = await _db.Users.FindAsync(guid)!,
                FirstName = dto.FirstName!,
                LastName = dto.LastName!,
                DateOfBirth = dto.DateOfBirth!.Value,
                RelationshipToUser = dto.RelationshipToUser!,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                LocationDisplayName = dto.LocationDisplayName,
                Occupation = dto.Occupation,
                ActivityLevel = dto.ActivityLevel,
                MedicalCondition = dto.MedicalCondition,
                SurgicalHistory = dto.SurgicalHistory,
                MedicationBeingTaken = dto.MedicationBeingTaken,
                CurrentComplaints = dto.CurrentComplaints,
                IsActive = dto.IsActive ?? true,
                IsOnboardingComplete = true
            };

            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProfile), new { id = patient.Id }, new
            {
                id = patient.Id,
                firstName = patient.FirstName,
                lastName = patient.LastName,
                dateOfBirth = patient.DateOfBirth,
                relationshipToUser = patient.RelationshipToUser,
                isActive = patient.IsActive
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();
            var guid = Guid.Parse(userId);

            var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == id && x.UserId == guid);
            if (p is null) return NotFound("Patient profile not found");

            return Ok(new
            {
                id = p.Id,
                firstName = p.FirstName,
                lastName = p.LastName,
                dateOfBirth = p.DateOfBirth,
                relationshipToUser = p.RelationshipToUser,
                address = p.Address,
                latitude = p.Latitude,
                longitude = p.Longitude,
                locationDisplayName = p.LocationDisplayName,
                occupation = p.Occupation,
                activityLevel = p.ActivityLevel,
                medicalCondition = p.MedicalCondition,
                surgicalHistory = p.SurgicalHistory,
                medicationBeingTaken = p.MedicationBeingTaken,
                currentComplaints = p.CurrentComplaints,
                isActive = p.IsActive,
                isOnboardingComplete = p.IsOnboardingComplete
            });
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> PatchProfile(int id, UpdatePatientProfileDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();
            var guid = Guid.Parse(userId);

            var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == id && x.UserId == guid);
            if (p is null) return NotFound("Patient profile not found");

            if (!string.IsNullOrWhiteSpace(dto.FirstName)) p.FirstName = dto.FirstName!;
            if (!string.IsNullOrWhiteSpace(dto.LastName)) p.LastName = dto.LastName!;
            if (dto.DateOfBirth is not null) p.DateOfBirth = dto.DateOfBirth.Value;
            if (!string.IsNullOrWhiteSpace(dto.RelationshipToUser)) p.RelationshipToUser = dto.RelationshipToUser!;
            if (!string.IsNullOrWhiteSpace(dto.Address)) p.Address = dto.Address!;
            if (dto.Latitude is not null) p.Latitude = dto.Latitude;
            if (dto.Longitude is not null) p.Longitude = dto.Longitude;
            if (!string.IsNullOrWhiteSpace(dto.LocationDisplayName)) p.LocationDisplayName = dto.LocationDisplayName!;
            if (!string.IsNullOrWhiteSpace(dto.Occupation)) p.Occupation = dto.Occupation!;
            if (!string.IsNullOrWhiteSpace(dto.ActivityLevel)) p.ActivityLevel = dto.ActivityLevel!;
            if (!string.IsNullOrWhiteSpace(dto.MedicalCondition)) p.MedicalCondition = dto.MedicalCondition!;
            if (!string.IsNullOrWhiteSpace(dto.SurgicalHistory)) p.SurgicalHistory = dto.SurgicalHistory!;
            if (!string.IsNullOrWhiteSpace(dto.MedicationBeingTaken)) p.MedicationBeingTaken = dto.MedicationBeingTaken!;
            if (!string.IsNullOrWhiteSpace(dto.CurrentComplaints)) p.CurrentComplaints = dto.CurrentComplaints!;

            if (dto.IsActive is not null) p.IsActive = dto.IsActive.Value;

            if (dto.SetAsActive == true)
            {
                p.IsActive = true;
                var others = await _db.Patients.Where(x => x.UserId == guid && x.Id != p.Id).ToListAsync();
                foreach (var o in others) o.IsActive = false;
            }

            await _db.SaveChangesAsync();

            return Ok(new { id = p.Id, isActive = p.IsActive });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();
            var guid = Guid.Parse(userId);

            var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == id && x.UserId == guid);
            if (p is null) return NotFound();

            p.IsActive = false;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
