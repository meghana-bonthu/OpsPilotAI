using Microsoft.AspNetCore.Identity;

namespace OpsPilot.Api.Data;

public static class IdentitySeeder
{
    private static readonly string[] Roles =
    {
        "Reporter",
        "Responder",
        "Administrator"
    };

    public static async Task SeedRolesAsync(
        IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<
                RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(
                    new IdentityRole(role));
            }
        }
    }
}