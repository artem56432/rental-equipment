using System.Security.Cryptography;
using System.Text;
using EquipmentRental.Models;
using EquipmentRental.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRental.Controllers;
public class HomeController(StoreService store) : Controller
{
    public IActionResult Index(string? q, int? category)
    {
        var items = store.Data.Equipment.Where(x => x.State != EquipmentState.WrittenOff);
        if (!string.IsNullOrWhiteSpace(q))
        {
            items = items.Where(x => (x.Name + " " + x.Description + " " + x.Tags).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (category > 0)
        {
            items = items.Where(x => x.CategoryId == category);
        }

        ViewBag.Categories = store.Data.Categories;
        ViewBag.Query = q;
        ViewBag.Category = category;
        return View(items.ToList());
    }

    public IActionResult Login() => View();
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Login(string email, string password)
    {
        var user = store.Data.Users.FirstOrDefault(x => x.Email.Equals(email ?? "", StringComparison.OrdinalIgnoreCase));
        if (user == null || !Verify(password ?? "", user.Password))
        {
            ModelState.AddModelError("", "Неверная почта или пароль");
            return View();
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        return RedirectToAction("Index");
    }

    public IActionResult Register() => View();
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Register(string name, string email, string password)
    {
        name = (name ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        if (name.Length < 2 || !email.Contains('@') || (password ?? "").Length < 8 || store.Data.Users.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("", "Введите имя, уникальную почту и пароль не короче 8 символов.");
            return View();
        }

        var user = new User
        {
            Id = store.NextId(store.Data.Users),
            Name = name,
            Email = email,
            Password = Hash(password!),
            Role = UserRole.Client
        };
        store.Data.Users.Add(user);
        store.Persist();
        HttpContext.Session.SetInt32("UserId", user.Id);
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string stored)
    {
        if (!stored.Contains(':'))
        {
            return password == stored;
        }

        try
        {
            var parts = stored.Split(':');
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[0]), 100000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
