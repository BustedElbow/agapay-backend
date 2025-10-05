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
    [Authorize]
    public class ContractsController : ControllerBase
    {
        private readonly agapayDbContext _db;
        public ContractsController(agapayDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateContractDto dto)
        {
            if (dto.EndDate <= dto.StartDate)
                return BadRequest("EndDate must be after StartDate");

            var patient = await _db.Patients.FindAsync(dto.PatientId);
            if (patient == null) return NotFound("Patient not found");
            var therapist = await _db.PhysicalTherapists.FindAsync(dto.PhysicalTherapistId);
            if (therapist == null) return NotFound("Therapist not found");

            // Optionally enforce that caller is either patient user or therapist user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var callerGuid = Guid.Parse(userId);
            var isPatientUser = patient.UserId == callerGuid;
            var isTherapistUser = therapist.UserId == callerGuid;
            if (!User.IsInRole("Admin") && !isPatientUser && !isTherapistUser)
                return Forbid();

            var contract = new Contract
            {
                PatientId = dto.PatientId,
                PhysicalTherapistId = dto.PhysicalTherapistId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Status = ContractStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _db.Contracts.Add(contract);
            await _db.SaveChangesAsync();
            return Ok(new { contract.Id });
        }

        [HttpGet("patient/{patientId:int}")]
        public async Task<IActionResult> GetForPatient(int patientId)
        {
            var contracts = await _db.Contracts
                .Where(c => c.PatientId == patientId)
                .Select(c => new { c.Id, c.PatientId, c.PhysicalTherapistId, c.StartDate, c.EndDate, c.Status })
                .ToListAsync();
            return Ok(contracts);
        }

        [HttpGet("therapist/{therapistId:int}")]
        public async Task<IActionResult> GetForTherapist(int therapistId)
        {
            var contracts = await _db.Contracts
                .Where(c => c.PhysicalTherapistId == therapistId)
                .Select(c => new { c.Id, c.PatientId, c.PhysicalTherapistId, c.StartDate, c.EndDate, c.Status })
                .ToListAsync();
            return Ok(contracts);
        }

        [HttpPut("{contractId:int}/status")]
        public async Task<IActionResult> UpdateStatus(int contractId, [FromBody] ContractStatus status)
        {
            var contract = await _db.Contracts.FindAsync(contractId);
            if (contract == null) return NotFound();
            contract.Status = status;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
