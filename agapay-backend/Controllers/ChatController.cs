using agapay_backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace agapay_backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly agapayDbContext _context;

        public ChatController(agapayDbContext context)
        {
            _context = context;
        }

        [HttpGet("history/{otherUserId}")]
        public async Task<IActionResult> GetConversationHistory(string otherUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(currentUserId, out var currentUserGuid) || !Guid.TryParse(otherUserId, out var otherUserGuid))
            {
                return BadRequest("Invalid user ID format.");
            }

            var messages = await _context.ChatMessages
                .Where(m => (m.SenderId == currentUserGuid && m.ReceiverId == otherUserGuid) ||
                             (m.SenderId == otherUserGuid && m.ReceiverId == currentUserGuid))
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.SenderId, m.Content, m.Timestamp })
                .ToListAsync();

            return Ok(messages);
        }
    }
}