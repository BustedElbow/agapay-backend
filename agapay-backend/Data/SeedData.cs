using agapay_backend.Entities;
using Microsoft.AspNetCore.Identity;

namespace agapay_backend.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

            // Seed Roles
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new Role { Name = roleName });
                }
            }

            // Seed Admin User
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
    }
}
