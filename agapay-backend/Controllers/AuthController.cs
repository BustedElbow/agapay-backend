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
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          Roles = roles.ToList(),
          UserType = "User", // User type is not yet determined
          IsPatientOnboardingComplete = false,
          IsTherapistOnboardingComplete = false
        }
      });
    }

    [HttpPost("register/patient")]
    public async Task<IActionResult> RegisterPatient(RegisterDto registerDto)
    {
      // If email exists, instruct to login and enroll
      var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
      if (existingUser != null)
      {
        return Conflict(new { message = "Account already exists. Please login and use enroll endpoint.", enrollEndpoint = "/api/auth/enroll/patient" });
      }
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

      await _userManager.AddToRoleAsync(user, "Patient");

      var patient = new Patient
      {
        UserId = user.Id,
        User = user,
        FirstName = user.FirstName,
        LastName = user.LastName,
        DateOfBirth = user.DateOfBirth,
        RelationshipToUser = "Self",
        IsActive = true,
        IsOnboardingComplete = false
      };

      _context.Patients.Add(patient);
      await _context.SaveChangesAsync();

      //Token
      var roles = await _userManager.GetRolesAsync(user);
      var accessToken = _tokenService.CreateAccessToken(user, roles);
      var refreshToken = _tokenService.CreateRefreshToken();
      user.RefreshToken = refreshToken;
      user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
      await _userManager.UpdateAsync(user);

      var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

      return Ok(new AuthResponseDto
      {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        User = new UserDto
        {
          Id = user.Id,
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = "Patient",
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
        }
      });
    }

    [HttpPost("register/therapist")]
    public async Task<IActionResult> RegisterTherapist(TherapistRegisterDto dto)
    {
      if (string.IsNullOrWhiteSpace(dto.LicenseNumber))
      {
        return BadRequest("LicenseNumber is required");
      }

      var existingByEmail = await _userManager.FindByEmailAsync(dto.Email);
      if (existingByEmail != null)
      {
        return Conflict(new { message = "Account already exists. Please login and use enroll endpoint.", enrollEndpoint = "/api/auth/enroll/therapist" });
      }

      var user = new User
      {
        UserName = dto.Email,
        Email = dto.Email,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };

      var result = await _userManager.CreateAsync(user, dto.Password);
      if (!result.Succeeded)
      {
        return BadRequest(result.Errors);
      }

      var therapist = new PhysicalTherapist
      {
        UserId = user.Id,
        User = user,
        LicenseNumber = dto.LicenseNumber,
        WorkPhoneNumber = dto.WorkPhoneNumber,
        VerificationStatus = VerificationStatus.Pending,
        SubmittedAt = DateTime.UtcNow,
        YearsOfExperience = 0,
        IsOnboardingComplete = false,
        RatingCount = 0,
        AverageRating = null
      };
      _context.PhysicalTherapists.Add(therapist);
      await _context.SaveChangesAsync();

      // Tokens
      var roles = await _userManager.GetRolesAsync(user);
      var accessToken = _tokenService.CreateAccessToken(user, roles);
      var refreshToken = _tokenService.CreateRefreshToken();
      user.RefreshToken = refreshToken;
      user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
      await _userManager.UpdateAsync(user);

      var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

      return Ok(new AuthResponseDto
      {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        User = new UserDto
        {
          Id = user.Id,
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = "User", // will eventually become Physical Therapist
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
          PreferredRole = user.PreferredRole
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
              Email = user.Email!,
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
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = newRoles.ToList(),
          UserType = string.Join(", ", newRoles), // User can have multiple types now
          IsPatientOnboardingComplete = patientCompleteFinal,
          IsTherapistOnboardingComplete = therapistCompleteFinal,
          PreferredRole = user.PreferredRole
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
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = userType,
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
          PreferredRole = user.PreferredRole
        }
      });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenDto dto)
    {
      if (string.IsNullOrWhiteSpace(dto.AccessToken) || string.IsNullOrWhiteSpace(dto.RefreshToken))
      {
        return BadRequest("Both access and refresh tokens are required.");
      }

      ClaimsPrincipal principal;
      try
      {
        principal = _tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);
      }
      catch
      {
        return Unauthorized("Invalid access token.");
      }

      var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null) return Unauthorized();

      if (!Guid.TryParse(userId, out var guidUserId))
      {
        return Unauthorized("Invalid user identifier in token.");
      }

      var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == guidUserId);
      if (user == null) return Unauthorized("User not found.");

      if (user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
      {
        return Unauthorized("Invalid or expired refresh token.");
      }

      var roles = await _userManager.GetRolesAsync(user);
      var newAccessToken = _tokenService.CreateAccessToken(user, roles);
      var newRefreshToken = _tokenService.CreateRefreshToken();

      user.RefreshToken = newRefreshToken;
      user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
      await _userManager.UpdateAsync(user);

      var userType = string.Join(", ", roles.Where(r => r != "User"));
      var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

      return Ok(new AuthResponseDto
      {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshToken,
        User = new UserDto
        {
          Id = user.Id,
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = userType,
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
          PreferredRole = user.PreferredRole
        }
      });
    }

    [HttpPost("login/patient")]
    public async Task<ActionResult<AuthResponseDto>> LoginPatient(LoginDto dto)
    {
      var user = await _userManager.FindByEmailAsync(dto.Email);
      if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
      {
        return Unauthorized("Invalid credentials");
      }

      var roles = await _userManager.GetRolesAsync(user);
      if (!roles.Contains("Patient"))
      {
        return Forbid("Account is not registered as a Patient");
      }

      var accessToken = _tokenService.CreateAccessToken(user, roles);
      var refreshToken = _tokenService.CreateRefreshToken();
      user.RefreshToken = refreshToken;
      user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
      await _userManager.UpdateAsync(user);

      var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

      return Ok(new AuthResponseDto
      {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        User = new UserDto
        {
          Id = user.Id,
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = "Patient",
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
          PreferredRole = user.PreferredRole
        }
      });
    }

    [HttpPost("login/therapist")]
    public async Task<ActionResult> LoginTherapist(LoginDto dto)
    {
      var user = await _userManager.FindByEmailAsync(dto.Email);
      if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
      {
        return Unauthorized("Invalid credentials");
      }

      var roles = await _userManager.GetRolesAsync(user);
      if (roles.Contains("PhysicalTherapist"))
      {
        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var refreshToken = _tokenService.CreateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationDays"]));
        await _userManager.UpdateAsync(user);

        var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);

        return Ok(new AuthResponseDto
        {
          AccessToken = accessToken,
          RefreshToken = refreshToken,
          User = new UserDto
          {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DateOfBirth = user.DateOfBirth,
            Roles = roles.ToList(),
            UserType = "PhysicalTherapist",
            IsPatientOnboardingComplete = patientComplete,
            IsTherapistOnboardingComplete = therapistComplete,
            PreferredRole = user.PreferredRole
          }
        });
      }

      var therapist = await _context.PhysicalTherapists.AsNoTracking().FirstOrDefaultAsync(pt => pt.UserId == user.Id);
      if (therapist is null)
      {
        return Forbid("No therapist applicatoin Found. Please sign up.");
      }

      return StatusCode(StatusCodes.Status403Forbidden, new
      {
        message = "Therapist account not verified yet.",
        status = therapist.VerificationStatus.ToString(),
        submittedAt = therapist.SubmittedAt,
        verifiedAt = therapist.VerifiedAt,
        rejectionReason = therapist.RejectionReason
      });
    }

    // Enroll existing authenticated user as Patient (idempotent)
    [HttpPost("enroll/patient")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> EnrollPatient()
    {
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null) return Unauthorized();
      var user = await _userManager.FindByIdAsync(userId);
      if (user == null) return Unauthorized();

      var roles = await _userManager.GetRolesAsync(user);
      bool added = false;
      if (!roles.Contains("Patient"))
      {
        await _userManager.AddToRoleAsync(user, "Patient");
        added = true;
      }

      if (!await _context.Patients.AnyAsync(p => p.UserId == user.Id))
      {
        _context.Patients.Add(new Patient
        {
          UserId = user.Id,
          User = user,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          RelationshipToUser = "Self",
          IsActive = true,
          IsOnboardingComplete = false
        });
        await _context.SaveChangesAsync();
      }

      if (added)
      {
        roles = await _userManager.GetRolesAsync(user);
      }

      var (patientComplete, therapistComplete) = await GetOnboardingStatus(user.Id);
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
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = string.Join(", ", roles),
          IsPatientOnboardingComplete = patientComplete,
          IsTherapistOnboardingComplete = therapistComplete,
          PreferredRole = user.PreferredRole
        }
      });
    }

    // Enroll existing authenticated user as Therapist Applicant (does not grant PhysicalTherapist yet)
    [HttpPost("enroll/therapist")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> EnrollTherapist(EnrollTherapistDto dto)
    {
      if (string.IsNullOrWhiteSpace(dto.LicenseNumber)) return BadRequest("LicenseNumber required");
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null) return Unauthorized();
      var user = await _userManager.FindByIdAsync(userId);
      if (user == null) return Unauthorized();

      // If therapist profile exists return existing state
      var therapist = await _context.PhysicalTherapists.FirstOrDefaultAsync(t => t.UserId == user.Id);
      if (therapist == null)
      {
        therapist = new PhysicalTherapist
        {
          UserId = user.Id,
          User = user,
          LicenseNumber = dto.LicenseNumber,
          WorkPhoneNumber = dto.WorkPhoneNumber,
          VerificationStatus = VerificationStatus.Pending,
          SubmittedAt = DateTime.UtcNow,
          YearsOfExperience = 0,
          IsOnboardingComplete = false,
          RatingCount = 0,
          AverageRating = null
        };
        _context.PhysicalTherapists.Add(therapist);
        await _context.SaveChangesAsync();
      }

      // Do NOT add PhysicalTherapist role until verified (Admin flow)
      var roles = await _userManager.GetRolesAsync(user);
      var (patientComplete2, therapistComplete2) = await GetOnboardingStatus(user.Id);
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
          Email = user.Email!,
          FirstName = user.FirstName,
          LastName = user.LastName,
          DateOfBirth = user.DateOfBirth,
          Roles = roles.ToList(),
          UserType = string.Join(", ", roles),
          IsPatientOnboardingComplete = patientComplete2,
          IsTherapistOnboardingComplete = therapistComplete2,
          PreferredRole = user.PreferredRole
        }
      });
    }

    [HttpPatch("preferred-role")]
    [Authorize]
    public async Task<ActionResult> SetPreferredRole(PreferredRoleDto dto)
    {
      if (string.IsNullOrWhiteSpace(dto.PreferredRole)) return BadRequest("PreferredRole required");
      var allowed = new[] { "Patient", "PhysicalTherapist" };
      if (!allowed.Contains(dto.PreferredRole)) return BadRequest("Invalid preferred role");

      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null) return Unauthorized();
      var user = await _userManager.FindByIdAsync(userId);
      if (user == null) return Unauthorized();

      var roles = await _userManager.GetRolesAsync(user);
      if (!roles.Contains(dto.PreferredRole)) return BadRequest("User does not have this role");

      user.PreferredRole = dto.PreferredRole;
      await _userManager.UpdateAsync(user);
      return NoContent();
    }

    private async Task<(bool patientComplete, bool therapistComplete)> GetOnboardingStatus(Guid userId)
    {
      var patientComplete = await _context.Patients.AnyAsync(p => p.UserId == userId && p.IsOnboardingComplete);

      var therapist = await _context.PhysicalTherapists.FirstOrDefaultAsync(pt => pt.UserId == userId);
      bool therapistComplete = therapist is not null && therapist.IsOnboardingComplete;

      return (patientComplete, therapistComplete);
    }
  }
}
