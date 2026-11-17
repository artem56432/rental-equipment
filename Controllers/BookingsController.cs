using System.Security.Cryptography;
using System.Text;
using EquipmentRental.Models;
using EquipmentRental.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRental.Controllers;
public class BookingsController(StoreService store) : Controller
{
    private User? Current => store.Data.Users.FirstOrDefault(user => user.Id == HttpContext.Session.GetInt32("UserId"));

    public IActionResult Index()
    {
        if (Current == null)
        {
            return RedirectToAction("Login", "Home");
        }

        ViewBag.Items = store.Data.Equipment;
        return View(store.Data.Bookings.Where(booking => booking.UserId == Current.Id).OrderByDescending(booking => booking.CreatedAt).ToList());
    }

    public IActionResult Create(int id)
    {
        if (Current == null)
        {
            return RedirectToAction("Login", "Home");
        }

        var item = store.Data.Equipment.FirstOrDefault(x => x.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(int id, DateTime startDate, DateTime endDate)
    {
        if (Current == null)
        {
            return RedirectToAction("Login", "Home");
        }

        var item = store.Data.Equipment.FirstOrDefault(x => x.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        lock (store)
        {
            if (startDate.Date < DateTime.Today || endDate.Date < startDate.Date || (endDate - startDate).TotalDays > 365 || item.State != EquipmentState.Available || !store.IsAvailable(id, startDate, endDate))
            {
                ModelState.AddModelError("", "Проверьте даты: оборудование недоступно на выбранный период или указан неверный срок.");
                return View(item);
            }

            store.Data.Bookings.Add(new Booking { Id = store.NextId(store.Data.Bookings), EquipmentId = id, UserId = Current.Id, StartDate = startDate.Date, EndDate = endDate.Date, TotalPrice = store.Price(id, startDate, endDate), DailyRate = item.PricePerDay });
            store.Persist();
        }

        TempData["Message"] = "Бронирование создано! Менеджер свяжется с вами для подтверждения.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Cancel(int id)
    {
        if (Current == null)
        {
            return RedirectToAction("Login", "Home");
        }

        var booking = store.Data.Bookings.FirstOrDefault(x => x.Id == id && x.UserId == Current.Id);
        if (booking?.Status is BookingStatus.Booked or BookingStatus.Confirmed)
        {
            booking.Status = BookingStatus.Cancelled;
            store.Persist();
        }

        return RedirectToAction("Index");
    }
}
