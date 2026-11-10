using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Models;

namespace TP_pharmacy.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await services.GetRequiredService<PharmacyDbContext>().Database.MigrateAsync();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var name in new[] { "Customer", "Seller", "Manager" })
        {
            if (await roles.RoleExistsAsync(name)) continue;
            var result = await roles.CreateAsync(new IdentityRole(name));
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        var config = services.GetRequiredService<IConfiguration>();
        // ТЕСТОВЫЙ МЕНЕДЖЕР
        // TODO: УДАЛИТЬ В ФИНАЛЬНОЙ ВЕРСИИ
        var email = "test_manager@mail.com";
        var password = "Password123";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        var users = services.GetRequiredService<UserManager<User>>();
        if (await users.FindByEmailAsync(email) != null) return;
        var db = services.GetRequiredService<PharmacyDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new User { UserName = email, Email = email, FullName = "Менеджер", UserType = "Manager" };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        var assigned = await users.AddToRoleAsync(user, "Manager");
        if (!assigned.Succeeded) throw new InvalidOperationException("Не удалось назначить роль менеджера.");
        db.Managers.Add(new Manager { UserId = user.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
