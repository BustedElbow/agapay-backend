using agapay_backend.Entities;
using Microsoft.AspNetCore.Identity;
using System.Net.NetworkInformation;

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
                        "Peripheral Nerve Injury",
                        "Carpal Tunnel Syndrome"
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
                        "Gait and Balance Problems",
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
                    "San Pedro", "Tibungco", "Catalunan Grande", "Catalunan Pequeño",
                    "Ma-a", "Bunawan", "Lasang", "Dumoy", "Bago Aplaya", "Tacunan",
                    "Ilang", "Waan", "Suba", "Lamanan", "Tamugan", "Marilog",
                    "Baracatan", "Carmen", "Communal", "Leon Garcia", "Langub",
                    "Malamba", "New Carmen", "Santo Tomas", "Sibulan", "Tampakan",
                    "Talandang", "Mudiang", "Daliaon", "Mandug", "Datu Salumay",
                    "Biao Joaquin", "Biao Guianga", "Kilate", "Crossing Bayabas",
                    "Sto. Niño", "Gumitan", "Pangyan", "Inayawan", "Magtuod",
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
    }
}
