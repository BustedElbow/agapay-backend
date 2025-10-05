using System;
using agapay_backend.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace agapay_backend.Data
{
  public class agapayDbContext(DbContextOptions<agapayDbContext> options) : IdentityDbContext<User, Role, Guid>(options)
  {
    public DbSet<Patient> Patients { get; set; }
    public DbSet<PhysicalTherapist> PhysicalTherapists { get; set; }
    public DbSet<Specialization> Specializations { get; set; }
    public DbSet<ConditionTreated> ConditionsTreated { get; set; }
    public DbSet<ServiceArea> ServiceAreas { get; set; }
    public DbSet<TherapistAvailability> TherapistAvailabilities { get; set; }
    public DbSet<PatientPreferences> PatientPreferences { get; set; }

    // New sets
    public DbSet<TherapySession> TherapySessions { get; set; }
    public DbSet<TherapistRating> TherapistRatings { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Contract> Contracts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<User>()
          .HasMany(u => u.Patients)
          .WithOne(p => p.User)
          .HasForeignKey(p => p.UserId);

      modelBuilder.Entity<PhysicalTherapist>()
          .HasMany(pt => pt.Specializations)
          .WithMany(s => s.PhysicalTherapists)
          .UsingEntity(j => j.ToTable("PhysicalTherapistSpecializations"));

      modelBuilder.Entity<PhysicalTherapist>()
          .HasMany(pt => pt.ConditionsTreated)
          .WithMany(ct => ct.PhysicalTherapists)
          .UsingEntity(j => j.ToTable("PhysicalTherapistConditions"));

      modelBuilder.Entity<PhysicalTherapist>()
          .HasMany(pt => pt.ServiceAreas)
          .WithMany(sa => sa.PhysicalTherapists)
          .UsingEntity(j => j.ToTable("PhysicalTherapistServiceAreas"));

      // Availability relationships
      modelBuilder.Entity<TherapistAvailability>()
          .HasOne(ta => ta.PhysicalTherapist)
          .WithMany(pt => pt.Availabilities)
          .HasForeignKey(ta => ta.PhysicalTherapistId);

      modelBuilder.Entity<PatientPreferences>()
          .HasOne(pp => pp.Patient)
          .WithOne(p => p.Preferences)
          .HasForeignKey<PatientPreferences>(pp => pp.PatientId);

      // Contracts
      modelBuilder.Entity<Contract>()
          .HasOne(c => c.Patient)
          .WithMany()
          .HasForeignKey(c => c.PatientId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<Contract>()
          .HasOne(c => c.PhysicalTherapist)
          .WithMany()
          .HasForeignKey(c => c.PhysicalTherapistId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<Contract>()
          .HasMany(c => c.Sessions)
          .WithOne(s => s.Contract)
          .HasForeignKey(s => s.ContractId)
          .OnDelete(DeleteBehavior.Cascade);

      // Sessions
      modelBuilder.Entity<TherapySession>()
          .HasOne(s => s.PhysicalTherapist)
          .WithMany()
          .HasForeignKey(s => s.PhysicalTherapistId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<TherapySession>()
          .HasOne(s => s.Patient)
          .WithMany()
          .HasForeignKey(s => s.PatientId)
          .OnDelete(DeleteBehavior.Cascade);

      // Ratings
      modelBuilder.Entity<TherapistRating>()
          .HasOne(r => r.PhysicalTherapist)
          .WithMany()
          .HasForeignKey(r => r.PhysicalTherapistId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<TherapistRating>()
          .HasOne(r => r.Patient)
          .WithMany()
          .HasForeignKey(r => r.PatientId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<TherapistRating>()
          .HasOne(r => r.Session)
          .WithMany()
          .HasForeignKey(r => r.SessionId)
          .OnDelete(DeleteBehavior.SetNull);

      // Conversations
      modelBuilder.Entity<Conversation>()
          .HasOne(c => c.ParticipantA)
          .WithMany()
          .HasForeignKey(c => c.ParticipantAId)
          .OnDelete(DeleteBehavior.Restrict);

      modelBuilder.Entity<Conversation>()
          .HasOne(c => c.ParticipantB)
          .WithMany()
          .HasForeignKey(c => c.ParticipantBId)
          .OnDelete(DeleteBehavior.Restrict);

      modelBuilder.Entity<Conversation>()
          .HasMany(c => c.Messages)
          .WithOne(m => m.Conversation)
          .HasForeignKey(m => m.ConversationId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<Conversation>()
          .HasIndex(c => new { c.ParticipantAId, c.ParticipantBId })
          .IsUnique();

      // Chat
      modelBuilder.Entity<ChatMessage>()
          .HasOne(m => m.Sender)
          .WithMany()
          .HasForeignKey(m => m.SenderId)
          .OnDelete(DeleteBehavior.Restrict);

      modelBuilder.Entity<ChatMessage>()
          .HasOne(m => m.Receiver)
          .WithMany()
          .HasForeignKey(m => m.ReceiverId)
          .OnDelete(DeleteBehavior.Restrict);

      // Ensure one Patient profile per user
      modelBuilder.Entity<Patient>()
          .HasIndex(p => p.UserId)
          .IsUnique();

      // Ensure one PhysicalTherapist profile per user
      modelBuilder.Entity<PhysicalTherapist>()
          .HasIndex(t => t.UserId)
          .IsUnique();

      // Limit PreferredRole length
      modelBuilder.Entity<User>()
          .Property(u => u.PreferredRole)
          .HasMaxLength(64);
    }
  }
}

