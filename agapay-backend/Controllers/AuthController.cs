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
    public class AuthController : ControllerBase
    {
        private readonly agapayDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthController(agapayDbContext context, UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService, IConfiguration config, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _tokenService = tokenService;
            _config = config;
            _passwordHasher = passwordHasher;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            var user = new User
            {
                UserName = registerDto.Email,
                Email = registerDto.Email,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                DateOfBirth = registerDto.DateOfBirth, 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            await _userManager.AddToRoleAsync(user, "User"); // Assign a temporary default role

            // Generate tokens for immediate login to proceed to the next step
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.CreateAccessToken(user, roles);
            var refreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
            await _userManager.UpdateAsync(user);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles.ToList(),
                    UserType = "User", // User type is not yet determined
                    IsPatientOnboardingComplete = false,
                    IsTherapistOnboardingComplete = false
                }
            });
        }

        [HttpPost("select-user-type")]
        [Authorize]
        public async Task<ActionResult<AuthResponseDto>> SelectUserType(UserTypeSelectionDto userTypeDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            string targetRole = userTypeDto.UserType; 
            if (targetRole != "Patient" && targetRole != "PhysicalTherapist")
            {
                return BadRequest("Invalid user type specified.");
            }

            if (targetRole == "Patient")
            {
                var currentRoles = await _userManager.GetRolesAsync(user);

                // If user already has the Patient role, treat this as a no-op and return 200 OK with the current user payload.
                // Do NOT update tokens or refresh-token metadata in that case.
                if (currentRoles.Contains("Patient"))
                {
                    var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

                    return Ok(new AuthResponseDto
                    {
                        AccessToken = string.Empty,
                        RefreshToken = string.Empty,
                        User = new UserDto
                        {
                            Id = user.Id,
                            Email = user.Email,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            DateOfBirth = user.DateOfBirth,
                            Roles = currentRoles.ToList(),
                            UserType = string.Join(", ", currentRoles),
                            IsPatientOnboardingComplete = patientComplete,
                            IsTherapistOnboardingComplete = therapistComplete
                        }
                    });
                }

                await _userManager.AddToRoleAsync(user, "Patient");

                if (currentRoles.Contains("User"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "User");
                }

                if (!await _context.Patients.AnyAsync(p => p.UserId == user.Id))
                {
                    var patient = new Patient
                    {
                        UserId = user.Id,
                        User = user,
                        IsActive = true,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        DateOfBirth = user.DateOfBirth,
                        RelationshipToUser = "Self",
                        IsOnboardingComplete = false
                    };
                    _context.Patients.Add(patient);
                }
            }
            else if (targetRole == "PhysicalTherapist")
            {
                // DO NOT create PhysicalTherapist record here or add the "PhysicalTherapist" role.
                // PhysicalTherapist records must be created when the user submits license info (and the record
                // must be created only once). Admin will verify and then the AdminController will add the
                // "PhysicalTherapist" role upon approval.
                //
                // If you want to capture that the user *requested* to be a therapist, implement a separate flag
                // / table. For now, simply acknowledge the request and instruct client to submit license.
            }

            await _context.SaveChangesAsync();

            // *** ISSUE A NEW TOKEN WITH THE UPDATED ROLES (if any changed) ***
            var newRoles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _tokenService.CreateAccessToken(user, newRoles);
            var newRefreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
            await _userManager.UpdateAsync(user);

            var (patientCompleteFinal, therapistCompleteFinal) = await GetOnboardingStatus(user.Id);

            return Ok(new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    DateOfBirth = user.DateOfBirth,
                    Roles = newRoles.ToList(),
                    UserType = string.Join(", ", newRoles), // User can have multiple types now
                    IsPatientOnboardingComplete = patientCompleteFinal,
                    IsTherapistOnboardingComplete = therapistCompleteFinal,
                }
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
            {
                return Unauthorized("Invalid credentials.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.CreateAccessToken(user, roles);
            var refreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
            await _userManager.UpdateAsync(user);

            var userType = string.Join(", ", roles.Where(r => r != "User"));
            var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    DateOfBirth = user.DateOfBirth,
                    Roles = roles.ToList(),
                    UserType = userType,
                    IsPatientOnboardingComplete = patientComplete,
                    IsTherapistOnboardingComplete = therapistComplete
                }
            });
        }

        private async Task<(bool patientComplete, bool therapistComplete)> GetOnboardingStatus(Guid userId)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            bool patientComplete = patient is not null && patient.IsOnboardingComplete;

            var therapist = await _context.PhysicalTherapists.FirstOrDefaultAsync(pt => pt.UserId == userId);
            bool therapistComplete = therapist is not null && therapist.IsOnboardingComplete;

            return (patientComplete, therapistComplete);
        }
    }
}
