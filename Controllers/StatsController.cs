using System.Security.Cryptography;
using System.Text;
using EquipmentRental.Models;
using EquipmentRental.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRental.Controllers;
public class StatsController(StoreService store) : Controller
{
    public IActionResult Index()
    {
        if (!store.Data.Users.Any(u => u.Id == HttpContext.Session.GetInt32("UserId") && u.Role == UserRole.Admin))
        {
            return StatusCode(403);
        }

        ViewBag.Items = store.Data.Equipment;
        return View(store.Data.Bookings);
    }
}
