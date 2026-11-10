using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Manager;

[Authorize(Roles = "Manager")]
public class EmployeesModel(PharmacyDbContext context, UserManager<User> users) : PageModel
{
    public List<Models.Seller> Employees { get; set; } = new();
    public List<Pharmacy> Pharmacies { get; set; } = new();
    [BindProperty] public EmployeeInput Input { get; set; } = new();
    public class EmployeeInput
    {
        public string? Id { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, Phone, StringLength(20)] public string PhoneNumber { get; set; } = "";
        [DataType(DataType.Password)] public string? Password { get; set; }
        [Range(1, int.MaxValue)] public int PharmacyId { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(string? id)
    {
        if (id != null)
        {
            var employee = await context.Sellers.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == id);
            if (employee == null) return NotFound();
            Input = new EmployeeInput { Id = id, FullName = employee.User.FullName, Email = employee.User.Email!,
                PhoneNumber = employee.User.PhoneNumber ?? "", PharmacyId = employee.PharmacyId };
        }
        await LoadAsync(); return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (Input.Id == null && string.IsNullOrWhiteSpace(Input.Password)) ModelState.AddModelError("Input.Password", "Задайте пароль нового сотрудника.");
        if (Input.Id != null && !string.IsNullOrEmpty(Input.Password)) ModelState.AddModelError("Input.Password", "Изменение пароля на этой странице не поддерживается.");
        if (!await context.Pharmacies.AnyAsync(p => p.Id == Input.PharmacyId)) ModelState.AddModelError("Input.PharmacyId", "Аптека не найдена.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        await using var transaction = await context.Database.BeginTransactionAsync();
        var seller = Input.Id == null ? new Models.Seller { User = new User { UserType = "Seller" } } :
            await context.Sellers.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == Input.Id);
        if (seller == null) return NotFound();
        var user = seller.User;
        user.FullName = Input.FullName.Trim(); user.Email = Input.Email.Trim(); user.UserName = user.Email;
        user.PhoneNumber = Input.PhoneNumber.Trim();
        var result = Input.Id == null ? await users.CreateAsync(user, Input.Password!) : await users.UpdateAsync(user);
        if (result.Succeeded && Input.Id == null) result = await users.AddToRoleAsync(user, "Seller");
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            await LoadAsync(); return Page();
        }
        seller.UserId = user.Id; seller.PharmacyId = Input.PharmacyId;
        if (Input.Id == null) context.Sellers.Add(seller);
        await context.SaveChangesAsync(); await transaction.CommitAsync(); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var seller = await context.Sellers.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == id);
        if (seller == null) return NotFound();
        ModelState.Clear();
        if (await context.Purchases.AnyAsync(p => p.SellerId == id) || await context.PrescriptionOrders.AnyAsync(p => p.SellerId == id))
            ModelState.AddModelError("", "Нельзя удалить сотрудника с историей продаж или рецептурных заказов.");
        else
        {
            // Remove only the seller profile/role, preserving any other user profiles.
            await using var transaction = await context.Database.BeginTransactionAsync();
            var result = await users.RemoveFromRoleAsync(seller.User, "Seller");
            if (!result.Succeeded)
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            else
            {
                var stampResult = await users.UpdateSecurityStampAsync(seller.User);
                if (!stampResult.Succeeded)
                {
                    foreach (var error in stampResult.Errors) ModelState.AddModelError("", error.Description);
                    await LoadAsync(); return Page();
                }
                context.Sellers.Remove(seller);
                seller.User.UserType = null;
                await context.SaveChangesAsync(); await transaction.CommitAsync(); return RedirectToPage();
            }
        }
        await LoadAsync(); return Page();
    }
    private async Task LoadAsync()
    {
        Employees = await context.Sellers.AsNoTracking().Include(s => s.User).Include(s => s.Pharmacy).OrderBy(s => s.User.FullName).ToListAsync();
        Pharmacies = await context.Pharmacies.AsNoTracking().OrderBy(p => p.Address).ToListAsync();
    }
}
