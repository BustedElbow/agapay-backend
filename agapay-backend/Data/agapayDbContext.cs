using agapay_backend.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace agapay_backend.Data
{
    public class agapayDbContext(DbContextOptions<agapayDbContext> options) : IdentityDbContext<User, Role, Guid> (options)
    {
        public DbSet<Patient> Patients { get; set; }
        public DbSet<PhysicalTherapist> PhysicalTherapists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

           
        }
    }
}
