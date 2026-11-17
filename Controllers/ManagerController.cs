using System.Security.Cryptography;
using System.Text;
using EquipmentRental.Models;
using EquipmentRental.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRental.Controllers;
public class ManagerController(StoreService store) : Controller
{
    private bool Allowed => store.Data.Users.Any(user => user.Id == HttpContext.Session.GetInt32("UserId") && user.Role is UserRole.Manager or UserRole.Admin);

    public IActionResult Index()
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        ViewBag.Users = store.Data.Users;
        ViewBag.Items = store.Data.Equipment;
        ViewBag.Store = store;
        foreach (var booking in store.Data.Bookings.Where(booking => booking.Status == BookingStatus.Issued && booking.EndDate.Date < DateTime.Today))
        {
            booking.Status = BookingStatus.Overdue;
        }

        store.Persist();
        return View(store.Data.Bookings.OrderByDescending(x => x.CreatedAt).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int id, string action, string? condition, decimal damageFine = 0, int unitId = 0)
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        var booking = store.Data.Bookings.FirstOrDefault(x => x.Id == id);
        if (booking == null)
        {
            return NotFound();
        }

        if (action == "confirm" && booking.Status == BookingStatus.Booked)
        {
            booking.Status = BookingStatus.Confirmed;
        }

        if (action == "issue" && booking.Status == BookingStatus.Confirmed)
        {
            lock (store)
            {
                if (!store.AssignUnit(booking.Id, booking.EquipmentId, unitId))
                {
                    TempData["Message"] = "Выберите свободный исправный экземпляр.";
                    return RedirectToAction("Index");
                }

                booking.Status = BookingStatus.Issued;
                store.Persist();
            }

            return RedirectToAction("Index");
        }

        if (action == "return" && (booking.Status is BookingStatus.Issued or BookingStatus.Overdue))
        {
            booking.Status = BookingStatus.Returned;
            booking.ReturnCondition = condition;
            var item = store.Data.Equipment.First(x => x.Id == booking.EquipmentId);
            booking.Fine = Math.Max(0, (DateTime.Today - booking.EndDate.Date).Days) * (booking.DailyRate > 0 ? booking.DailyRate : item.PricePerDay) + Math.Max(0, damageFine);
        }

        store.Persist();
        return RedirectToAction("Index");
    }
}
