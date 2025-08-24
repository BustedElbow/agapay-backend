using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Models;
using agapay_backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        //private readonly agapayDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly IPasswordHasher<User> _passwordHasher;
        public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService, IConfiguration config, IPasswordHasher<User> passwordHasher)
        {
            //_context = context;
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            await _userManager.AddToRoleAsync(user, "User"); // Assign a default role
            return Ok(new { message = "Registration successful" });
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

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }
    }
}
