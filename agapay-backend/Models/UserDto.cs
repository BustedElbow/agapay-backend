namespace agapay_backend.Models
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public List<string> Roles { get; set; } = new();
        public string UserType { get; set; }
        public bool IsPatientOnboardingComplete { get; set; }
        public bool IsTherapistOnboardingComplete { get; set; }
    }
}
