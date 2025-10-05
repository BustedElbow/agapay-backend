using agapay_backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var env = serviceProvider.GetRequiredService<IHostEnvironment>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var context = serviceProvider.GetRequiredService<agapayDbContext>();

            // Seed Roles
            string[] roleNames = { "Admin", "User", "Patient", "PhysicalTherapist" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new Role { Name = roleName });
                }
            }

            // Seed Admin User (development by default; can be enabled in other envs with Seed:AdminUser=true)
            var seedAdmin = env.IsDevelopment() || config.GetValue<bool>("Seed:AdminUser");
            if (seedAdmin)
            {
                if (await userManager.FindByEmailAsync("admin@example.com") == null)
                {
                    var adminUser = new User
                    {
                        UserName = "admin@example.com",
                        Email = "admin@example.com",
                        FirstName = "Admin",
                        LastName = "User",
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(adminUser, "Password123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                }
            }

            //Seed here
            await SeedSpecializations(context);
            await SeedConditionsTreated(context);
            await SeedServiceAreas(context);
            await SeedRecommendationDemoData(context, userManager);
        }

        private static async Task SeedSpecializations(agapayDbContext context)
        {
            if (!context.Specializations.Any())
            {
                var specializations = new[]
                {
                    "Orthopedic",
                    "Pediatric",
                    "Geriatric",
                    "Neurological",
                    "Sports",
                    "Cardiopulmonary",
                };

                foreach (var specialization in specializations)
                {
                    context.Specializations.Add(new Specialization { Name = specialization });
                }

                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedConditionsTreated(agapayDbContext context)
        {
            if (!context.ConditionsTreated.Any())
            {
                var groupedConditions = new Dictionary<ConditionCategory, string[]>
                {
                    [ConditionCategory.Neurological] = new[]
                    {
                        "Stroke",
                        "Spinal Cord Injury",
                        "Traumatic Brain Injury",
                        "Radiculopathy",
                        "Sciatica",
                        "Bell's Palsy",
                        "Peripheral Nerve Injury (e.g. Carpal Tunnel Syndrome)"
                    },
                    [ConditionCategory.Musculoskeletal] = new[]
                    {
                        "Frozen Shoulder/Adhesive Capsulitis",
                        "Fracture",
                        "Scoliosis",
                        "Myofascial Pain Syndrome",
                        "Low Back Pain",
                        "Sprain/Strain",
                        "Ligament Tears (ACL, PCL, MCL)",
                        "Patellofemoral Pain Syndrome",
                        "Plantar Fasciitis"
                    },
                    [ConditionCategory.Pediatric] = new[]
                    {
                        "Global Developmental Delay",
                        "Cerebral Palsy",
                        "Spina Bifida",
                        "Down Syndrome"
                    },
                    [ConditionCategory.Geriatric] = new[]
                    {
                        "Arthritis",
                        "Osteoarthritis",
                        "Deconditioning and Generalized Weakness",
                        "Gait and Balance Problem",
                        "Parkinson's Disease"
                    },
                    [ConditionCategory.Cardiopulmonary] = Array.Empty<string>()
                };

                foreach (var kvp in groupedConditions)
                {
                    foreach (var condition in kvp.Value)
                    {
                        context.ConditionsTreated.Add(new ConditionTreated { Name = condition, Category = kvp.Key });
                    }
                }

                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedServiceAreas(agapayDbContext context)
        {
            if (!context.ServiceAreas.Any())
            {
                var barangays = new[]
                {
                     "Mintal", "Buhangin", "Roxas", "Agdao", "Paquibato", "Toril", "Tugbok",
                    "Calinan", "Baguio", "Cabantian", "Matina", "Talomo", "Poblacion",
                    "San Pedro", "Tibungco", "Catalunan Grande", "Catalunan Peque�o",
                    "Ma-a", "Bunawan", "Lasang", "Dumoy", "Bago Aplaya", "Tacunan",
                    "Ilang", "Waan", "Suba", "Lamanan", "Tamugan", "Marilog",
                    "Baracatan", "Carmen", "Communal", "Leon Garcia", "Langub",
                    "Malamba", "New Carmen", "Santo Tomas", "Sibulan", "Tampakan",
                    "Talandang", "Mudiang", "Daliaon", "Mandug", "Datu Salumay",
                    "Biao Joaquin", "Biao Guianga", "Kilate", "Crossing Bayabas",
                    "Sto. Ni�o", "Gumitan", "Pangyan", "Inayawan", "Magtuod",
                    "Arancia", "Buda", "Dalagdag", "Lacson", "Magsaysay",
                    "Malabog", "Marapangi", "Paradise Embac", "Riverside",
                    "Salapawan", "Subasta", "Vicente Hizon Sr.", "Wines",
                    "19-B", "21-C", "22-C", "23-C", "24-C", "25-C", "26-C",
                    "27-C", "28-C", "29-C", "30-C", "31-D", "32-D", "33-D",
                    "34-D", "35-D", "36-D", "37-D", "38-D", "39-D", "40-D",
                    "1-A", "2-A", "3-A", "4-A", "5-A", "6-A", "7-A", "8-A",
                    "9-A", "10-A", "11-B", "12-B", "13-B", "14-B", "15-B",
                    "16-B", "17-B", "18-B"
                };

                foreach (var barangay in barangays)
                {
                    context.ServiceAreas.Add(new ServiceArea { Name = barangay });
                }

                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedRecommendationDemoData(agapayDbContext context, UserManager<User> userManager)
        {
            const string markerLicense = "PT-A-DEMO";
            if (await context.PhysicalTherapists.AnyAsync(pt => pt.LicenseNumber == markerLicense))
            {
                return;
            }

            var orthopedic = await context.Specializations.FirstOrDefaultAsync(s => s.Name == "Orthopedic");
            var pediatric = await context.Specializations.FirstOrDefaultAsync(s => s.Name == "Pediatric");
            var sports = await context.Specializations.FirstOrDefaultAsync(s => s.Name == "Sports");
            var mintal = await context.ServiceAreas.FirstOrDefaultAsync(sa => sa.Name == "Mintal");
            var buhangin = await context.ServiceAreas.FirstOrDefaultAsync(sa => sa.Name == "Buhangin");

            var postStroke = await context.ConditionsTreated
                .FirstOrDefaultAsync(c => c.Name == "Post-stroke rehabilitation");
            if (postStroke is null)
            {
                postStroke = new ConditionTreated
                {
                    Name = "Post-stroke rehabilitation",
                    Category = ConditionCategory.Neurological
                };
                context.ConditionsTreated.Add(postStroke);
                await context.SaveChangesAsync();
            }

            if (orthopedic is null || pediatric is null || sports is null || mintal is null || buhangin is null)
            {
                return; // prerequisite reference data missing
            }

            var demoPatient = await EnsurePatientAsync("patient.demo@example.com", "Demo", "Patient", relationship: "Self");
            var patientEntity = demoPatient.patient;

            patientEntity.Barangay = "Mintal";
            patientEntity.IsOnboardingComplete = true;

            if (patientEntity.Preferences is null)
            {
                patientEntity.Preferences = new PatientPreferences
                {
                    Patient = patientEntity,
                    PreferredDayOfWeek = DayOfWeekEnum.Monday,
                    PreferredStartTime = new TimeOnly(8, 0),
                    PreferredEndTime = new TimeOnly(18, 0),
                    SessionBudget = 1000m,
                    PreferredSpecialization = "Orthopedic",
                    DesiredService = "Post-stroke rehabilitation",
                    PreferredBarangay = "Mintal",
                    PreferredTherapistGender = "Female"
                };
                context.PatientPreferences.Add(patientEntity.Preferences);
            }
            else
            {
                patientEntity.Preferences.PreferredDayOfWeek = DayOfWeekEnum.Monday;
                patientEntity.Preferences.PreferredStartTime = new TimeOnly(8, 0);
                patientEntity.Preferences.PreferredEndTime = new TimeOnly(18, 0);
                patientEntity.Preferences.SessionBudget ??= 1000m;
                patientEntity.Preferences.PreferredSpecialization ??= "Orthopedic";
                patientEntity.Preferences.DesiredService ??= "Post-stroke rehabilitation";
                patientEntity.Preferences.PreferredBarangay ??= "Mintal";
                patientEntity.Preferences.PreferredTherapistGender ??= "Female";
            }

            await context.SaveChangesAsync();

            // Create a pool of patient raters so each rating record has a unique patient source.
            var ratingPatients = new List<Patient>();
            for (var i = 1; i <= 55; i++)
            {
                var (_, raterPatient) = await EnsurePatientAsync($"demo.rater{i:D2}@example.com", $"Rater{i:D2}", "Tester", relationship: "Self");
                ratingPatients.Add(raterPatient);
            }

            var therapistA = await CreateTherapistAsync(
                email: "therapist.a@example.com",
                firstName: "Alice",
                lastName: "Anderson",
                licenseNumber: "PT-A-DEMO",
                yearsOfExperience: 6,
                feePerSession: 900m,
                specializations: new[] { orthopedic },
                serviceAreas: new[] { mintal },
                includeInRecommendations: true,
                conditionsTreated: new[] { postStroke });
                

            var therapistB = await CreateTherapistAsync(
                email: "therapist.b@example.com",
                firstName: "Bruno",
                lastName: "Bautista",
                licenseNumber: "PT-B-DEMO",
                yearsOfExperience: 13,
                feePerSession: 1125m,
                specializations: new[] { sports },
                serviceAreas: new[] { mintal },
                includeInRecommendations: true,
                conditionsTreated: new[] { postStroke });

            var therapistC = await CreateTherapistAsync(
                email: "therapist.c@example.com",
                firstName: "Clara",
                lastName: "Castro",
                licenseNumber: "PT-C-DEMO",
                yearsOfExperience: 11,
                feePerSession: 1495m,
                specializations: new[] { orthopedic },
                serviceAreas: new[] { buhangin },
                includeInRecommendations: true);

            var experienceAnchor = await CreateTherapistAsync(
                email: "therapist.d@example.com",
                firstName: "Diego",
                lastName: "Delos Santos",
                licenseNumber: "PT-D-MAX",
                yearsOfExperience: 16,
                feePerSession: 1200m,
                specializations: Array.Empty<Specialization>(),
                serviceAreas: new[] { buhangin },
                includeInRecommendations: false);

            // Availability blocks sized to achieve the target overlap percentages (Patient prefers Monday 8-18 = 600 minutes)
            context.TherapistAvailabilities.AddRange(
                new TherapistAvailability
                {
                    PhysicalTherapist = therapistA,
                    DayOfWeek = DayOfWeekEnum.Monday,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(16, 0),
                    IsAvailable = true
                },
                new TherapistAvailability
                {
                    PhysicalTherapist = therapistB,
                    DayOfWeek = DayOfWeekEnum.Monday,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(14, 0),
                    IsAvailable = true
                },
                new TherapistAvailability
                {
                    PhysicalTherapist = therapistC,
                    DayOfWeek = DayOfWeekEnum.Monday,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                    IsAvailable = true
                });

            // Ratings (use unique raters for deterministic aggregates)
            var raterIndex = 0;

            void AddRatings(PhysicalTherapist therapist, int fiveStarCount, int fourStarCount)
            {
                for (var i = 0; i < fiveStarCount; i++)
                {
                    var patient = ratingPatients[raterIndex++];
                    context.TherapistRatings.Add(new TherapistRating
                    {
                        PhysicalTherapist = therapist,
                        Patient = patient,
                        Score = 5,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                for (var i = 0; i < fourStarCount; i++)
                {
                    var patient = ratingPatients[raterIndex++];
                    context.TherapistRatings.Add(new TherapistRating
                    {
                        PhysicalTherapist = therapist,
                        Patient = patient,
                        Score = 4,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            AddRatings(therapistA, fiveStarCount: 8, fourStarCount: 7);   // 68 / 15 = 4.5333 -> normalized � 0.91
            AddRatings(therapistB, fiveStarCount: 23, fourStarCount: 12); // 163 / 35 = 4.6571 -> normalized � 0.93
            AddRatings(therapistC, fiveStarCount: 2, fourStarCount: 3);   // 22 / 5 = 4.4 -> normalized � 0.90

            // Update summary aggregates to match raw data
            therapistA.RatingCount = 15;
            therapistA.AverageRating = 68d / 15d;

            therapistB.RatingCount = 35;
            therapistB.AverageRating = 163d / 35d;

            therapistC.RatingCount = 5;
            therapistC.AverageRating = 22d / 5d;

            experienceAnchor.RatingCount = 0;
            experienceAnchor.AverageRating = null;

            await context.SaveChangesAsync();

            async Task<(User user, Patient patient)> EnsurePatientAsync(string email, string firstName, string lastName, string relationship)
            {
                var user = await EnsureUserAsync(email, firstName, lastName, roleName: "Patient");
                var patient = await context.Patients.Include(p => p.Preferences).FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (patient is null)
                {
                    patient = new Patient
                    {
                        UserId = user.Id,
                        User = user,
                        FirstName = firstName,
                        LastName = lastName,
                        DateOfBirth = new DateOnly(1990, 1, 1),
                        RelationshipToUser = relationship,
                        IsActive = true,
                        IsOnboardingComplete = true
                    };
                    context.Patients.Add(patient);
                    await context.SaveChangesAsync();
                    patient = await context.Patients.Include(p => p.Preferences).FirstAsync(p => p.Id == patient.Id);
                }

                patient.FirstName = firstName;
                patient.LastName = lastName;
                return (user, patient);
            }

            async Task<User> EnsureUserAsync(string email, string firstName, string lastName, string roleName)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    return user;
                }

                user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "Password123!");
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to seed user {email}: {errors}");
                }

                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    await userManager.AddToRoleAsync(user, roleName);
                }

                return user;
            }

            async Task<PhysicalTherapist> CreateTherapistAsync(
                string email,
                string firstName,
                string lastName,
                string licenseNumber,
                int yearsOfExperience,
                decimal feePerSession,
                IEnumerable<Specialization> specializations,
                IEnumerable<ServiceArea> serviceAreas,
                bool includeInRecommendations,
                IEnumerable<ConditionTreated>? conditionsTreated = null,
                string? otherConditionsTreated = null)
            {
                var user = await EnsureUserAsync(email, firstName, lastName, roleName: "PhysicalTherapist");
                var therapist = new PhysicalTherapist
                {
                    UserId = user.Id,
                    User = user,
                    LicenseNumber = licenseNumber,
                    VerificationStatus = VerificationStatus.Verified,
                    SubmittedAt = DateTime.UtcNow,
                    VerifiedAt = DateTime.UtcNow,
                    YearsOfExperience = yearsOfExperience,
                    FeePerSession = feePerSession,
                    IsOnboardingComplete = includeInRecommendations,
                    RatingCount = 0,
                    AverageRating = null
                };

                foreach (var spec in specializations)
                {
                    therapist.Specializations.Add(spec);
                }

                foreach (var area in serviceAreas)
                {
                    therapist.ServiceAreas.Add(area);
                }

                if (conditionsTreated != null)
                {
                    foreach (var cond in conditionsTreated)
                    {
                        therapist.ConditionsTreated.Add(cond);
                    }
                }

                if (!string.IsNullOrWhiteSpace(otherConditionsTreated))
                {
                    therapist.OtherConditionsTreated = otherConditionsTreated;
                }

                context.PhysicalTherapists.Add(therapist);
                return therapist;
            }
        }
    }
}


