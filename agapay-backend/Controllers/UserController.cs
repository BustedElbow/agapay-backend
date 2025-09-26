using agapay_backend.Data;
using agapay_backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly agapayDbContext _context;

        public UserController(agapayDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUsers()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.UserName,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.EmailConfirmed,
                    u.LockoutEnabled,
                    u.LockoutEnd,
                    u.TwoFactorEnabled,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

    }
}
