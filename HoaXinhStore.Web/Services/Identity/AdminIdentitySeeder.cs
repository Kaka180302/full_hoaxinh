using HoaXinhStore.Web.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Services.Identity;

public class AdminIdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminAccountOptions> adminOptions,
    ILogger<AdminIdentitySeeder> logger)
{
    private const string AdminRole = "Admin";
    private const string EditorRole = "Editor";

    public async Task SeedAsync()
    {
        await EnsureRoleAsync(AdminRole);
        await EnsureRoleAsync(EditorRole);

        var options = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("Admin account options are missing. Skipping admin user seed.");
            return;
        }

        var existingUser = await userManager.FindByEmailAsync(options.Email);
        if (existingUser is null)
        {
            var user = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                EmailConfirmed = true,
                FullName = options.FullName
            };

            var createResult = await userManager.CreateAsync(user, options.Password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning("Cannot create admin user: {Errors}", string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            existingUser = user;
        }
        else
        {
            var changed = false;

            if (!string.Equals(existingUser.UserName, options.Email, StringComparison.OrdinalIgnoreCase))
            {
                existingUser.UserName = options.Email;
                changed = true;
            }

            if (!string.Equals(existingUser.FullName, options.FullName, StringComparison.Ordinal))
            {
                existingUser.FullName = options.FullName;
                changed = true;
            }

            if (!existingUser.EmailConfirmed)
            {
                existingUser.EmailConfirmed = true;
                changed = true;
            }

            if (changed)
            {
                await userManager.UpdateAsync(existingUser);
            }

            var passwordOk = await userManager.CheckPasswordAsync(existingUser, options.Password);
            if (!passwordOk)
            {
                var remove = await userManager.RemovePasswordAsync(existingUser);
                if (!remove.Succeeded)
                {
                    logger.LogWarning("Cannot remove old admin password: {Errors}", string.Join("; ", remove.Errors.Select(e => e.Description)));
                }

                var add = await userManager.AddPasswordAsync(existingUser, options.Password);
                if (!add.Succeeded)
                {
                    logger.LogWarning("Cannot set configured admin password: {Errors}", string.Join("; ", add.Errors.Select(e => e.Description)));
                }
            }
        }

        if (!await userManager.IsInRoleAsync(existingUser, AdminRole))
        {
            await userManager.AddToRoleAsync(existingUser, AdminRole);
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
