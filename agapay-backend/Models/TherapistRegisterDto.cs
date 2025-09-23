namespace agapay_backend.Models
{
    public class TherapistRegisterDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string LicenseNumber { get; set; }
        public string? WorkPhoneNumber { get; set; }
    }
}
