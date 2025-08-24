using Microsoft.AspNetCore.Identity;

namespace agapay_backend.Entities
{
    public class User : IdentityUser<Guid>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Nav Props
        public Patient? Patient { get; set; }
        public PhysicalTherapist? PhysicalTherapist { get; set; }
    }
}
