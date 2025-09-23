namespace agapay_backend.Models
{
    public class RegisterDto
    {
        public string FirstName { get; set; } 
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public DateOnly DateOfBirth { get; set; }
    }
}
