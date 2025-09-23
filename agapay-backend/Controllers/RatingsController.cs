using agapay_backend.Models;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace agapay_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RatingsController : ControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingsController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> SubmitRating(SubmitRatingDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            await _ratingService.SubmitRatingAsync(dto, Guid.Parse(userId));
            return Ok();
        }

        [HttpGet("therapist/{therapistId}/score")]
        [Authorize]
        public async Task<IActionResult> GetTherapistScore(int therapistId)
        {
            var result = await _ratingService.ComputeNormalizedRatingAsync(therapistId);
            return Ok(new
            {
                normalizedScore = result.normalizedScore,
                rawBayes = result.rawBayes,
                ratingCount = result.n,
                average = result.avg,
                globalAverage = result.globalAvg,
                smoothingK = result.k
            });
        }
    }
}
