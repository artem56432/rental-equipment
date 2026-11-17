using System.Security.Cryptography;
using System.Text;
using EquipmentRental.Models;
using EquipmentRental.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRental.Controllers;
public class AdminController(StoreService store) : Controller
{
    private bool Allowed => store.Data.Users.Any(user => user.Id == HttpContext.Session.GetInt32("UserId") && user.Role == UserRole.Admin);

    public IActionResult Index()
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        ViewBag.Categories = store.Data.Categories;
        ViewBag.Users = store.Data.Users;
        ViewBag.Units = store.InventoryRows();
        ViewBag.UnitHistory = store.InventoryHistory();
        return View(store.Data.Equipment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(int id, string name, string description, int categoryId, decimal pricePerDay, int totalUnits, EquipmentState state, string imageUrl)
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        if (string.IsNullOrWhiteSpace(name) || pricePerDay <= 0 || totalUnits < 1 || !store.Data.Categories.Any(category => category.Id == categoryId))
        {
            TempData["Message"] = "Проверьте данные оборудования.";
            return RedirectToAction("Index");
        }

        var item = store.Data.Equipment.FirstOrDefault(x => x.Id == id);
        if (item == null)
        {
            item = new Equipment
            {
                Id = store.NextId(store.Data.Equipment)
            };
            store.Data.Equipment.Add(item);
        }

        item.Name = name.Trim();
        item.Description = description ?? "";
        item.CategoryId = categoryId;
        item.PricePerDay = pricePerDay;
        if (id == 0)
        {
            item.TotalUnits = totalUnits;
            item.AvailableUnits = totalUnits;
        }

        item.State = state;
        item.ImageUrl = imageUrl ?? "";
        store.Persist();
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Inventory(int id, int totalUnits, int repairUnits, int writtenOffUnits)
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        var item = store.Data.Equipment.FirstOrDefault(x => x.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        if (totalUnits < item.TotalUnits || repairUnits < 0 || writtenOffUnits < 0 || repairUnits + writtenOffUnits > totalUnits)
        {
            TempData["Message"] = "Некорректное количество единиц.";
            return RedirectToAction("Index");
        }

        lock (store)
        {
            if (store.HasIssuedUnits(id) && (repairUnits != item.RepairUnits || writtenOffUnits != item.WrittenOffUnits))
            {
                TempData["Message"] = "Сначала примите выданные экземпляры, затем измените состояние склада.";
                return RedirectToAction("Index");
            }

            int capacity = totalUnits - repairUnits - writtenOffUnits;
            var activeBookings = store.Data.Bookings.Where(booking => booking.EquipmentId == id && (booking.Status is BookingStatus.Booked or BookingStatus.Confirmed or BookingStatus.Issued or BookingStatus.Overdue)).ToList();
            if (activeBookings.Any(booking => activeBookings.Count(x => x.StartDate.Date <= booking.StartDate.Date && x.EndDate.Date >= booking.StartDate.Date) > capacity))
            {
                TempData["Message"] = "Количество исправных единиц меньше числа активных броней.";
                return RedirectToAction("Index");
            }

            item.TotalUnits = totalUnits;
            item.RepairUnits = repairUnits;
            item.WrittenOffUnits = writtenOffUnits;
            item.AvailableUnits = capacity;
            store.Persist();
        }

        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Category(int id, string name)
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return RedirectToAction("Index");
        }

        var category = store.Data.Categories.FirstOrDefault(x => x.Id == id);
        if (category == null)
        {
            category = new Category
            {
                Id = store.NextId(store.Data.Categories)
            };
            store.Data.Categories.Add(category);
        }

        category.Name = name.Trim();
        store.Persist();
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Role(int id, UserRole role)
    {
        if (!Allowed)
        {
            return StatusCode(403);
        }

        var user = store.Data.Users.FirstOrDefault(x => x.Id == id);
        if (user != null && user.Id != HttpContext.Session.GetInt32("UserId"))
        {
            user.Role = role;
            store.Persist();
        }

        return RedirectToAction("Index");
    }
}
