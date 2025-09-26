namespace agapay_backend.Models
{
    public class CreateSessionDto
    {
        public int TherapistId { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public string? LocationAddress { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? DoctorReferralImageUrl { get; set; }
        public decimal? TotalFee { get; set; }
        public decimal? PatientFee { get; set; }
    }
}

