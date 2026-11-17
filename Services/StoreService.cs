using System.Text.Json;
using EquipmentRental.Models;

namespace EquipmentRental.Services;
public class StoreService
{
    private readonly string _file; private readonly SqliteStore _db; private readonly object _lock = new(); private AppData _data;
    public StoreService(IWebHostEnvironment env) { _file = Path.Combine(env.ContentRootPath, "App_Data", "store.json"); Directory.CreateDirectory(Path.GetDirectoryName(_file)!); _db = new SqliteStore(Path.Combine(env.ContentRootPath, "App_Data", "renta.db")); _data = Load() ?? Seed(); foreach(var b in _data.Bookings) if(b.DailyRate<=0) b.DailyRate=_data.Equipment.FirstOrDefault(x=>x.Id==b.EquipmentId)?.PricePerDay ?? 0; Save(); }
    public AppData Data { get { lock (_lock) return _data; } }
    private AppData? Load() { try { var json=_db.Read(); if(json!=null) return JsonSerializer.Deserialize<AppData>(json) ?? throw new InvalidDataException("SQLite contains invalid data"); if (!File.Exists(_file)) return null; return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_file)); } catch (Exception ex) { throw new InvalidDataException("Не удалось прочитать данные. Исходный файл сохранён.", ex); } }
    private void Save() { lock (_lock) _db.Write(JsonSerializer.Serialize(_data)); }
    public void Persist() => Save();
    public bool IsAvailable(int equipmentId, DateTime start, DateTime end, int? ignoreBooking = null) {
        var item = _data.Equipment.First(e => e.Id == equipmentId);
        int capacity = item.TotalUnits - item.RepairUnits - item.WrittenOffUnits;
        if (capacity <= 0 || end.Date < start.Date || (end.Date-start.Date).TotalDays > 365) return false;
        var active = _data.Bookings.Where(b => b.EquipmentId == equipmentId && b.Id != ignoreBooking &&
            (b.Status is BookingStatus.Booked or BookingStatus.Confirmed or BookingStatus.Issued or BookingStatus.Overdue)).ToList();
        foreach (var day in Enumerable.Range(0, (end.Date-start.Date).Days+1).Select(n => start.Date.AddDays(n))) {
            int occupied = active.Count(b => b.StartDate.Date <= day &&
                ((b.Status is BookingStatus.Issued or BookingStatus.Overdue) && b.EndDate.Date < DateTime.Today ? DateTime.Today : b.EndDate.Date) >= day);
            if (occupied >= capacity) return false;
        }
        return true;
    }
    public decimal Price(int equipmentId, DateTime start, DateTime end) { var item = _data.Equipment.First(e => e.Id == equipmentId); var days = Math.Max(1, (end.Date - start.Date).Days + 1); return days * item.PricePerDay; }
    public int NextId<T>(IEnumerable<T> list) => list.Cast<object>().Count() == 0 ? 1 : list.Cast<dynamic>().Max(x => (int)x.Id) + 1;
    private static AppData Seed() => new() {
      Categories = [new Category { Id=1, Name="Спорт", Icon="⚡" }, new Category { Id=2, Name="Туризм", Icon="⛰️" }, new Category { Id=3, Name="Стройка", Icon="🛠️" }],
      Users = [new User { Id=1, Name="Анна Петрова", Email="anna@renta.ru", Password="demo", Role=UserRole.Client }, new User { Id=2, Name="Илья Смирнов", Email="manager@renta.ru", Password="demo", Role=UserRole.Manager }, new User { Id=3, Name="Администратор", Email="admin@renta.ru", Password="demo", Role=UserRole.Admin }],
      Equipment = [
       new Equipment { Id=1, Name="Велосипед Trek Marlin 7", Description="Горный велосипед для города и трейлов", CategoryId=1, PricePerDay=1500, TotalUnits=8, AvailableUnits=6, ImageUrl="/images/bike.jpg", Tags="велосипед,спорт" },
       new Equipment { Id=2, Name="Палатка Naturehike 2P", Description="Лёгкая двухместная палатка для походов", CategoryId=2, PricePerDay=1200, TotalUnits=12, AvailableUnits=10, ImageUrl="/images/tent.jpg", Tags="палатка,поход" },
       new Equipment { Id=3, Name="Перфоратор Bosch GBH 2-26", Description="Профессиональный перфоратор 800 Вт", CategoryId=3, PricePerDay=900, TotalUnits=5, AvailableUnits=4, ImageUrl="/images/hammer.jpg", Tags="инструмент,ремонт" },
       new Equipment { Id=4, Name="Сапборд Aqua Marina", Description="Надувная доска с веслом и насосом", CategoryId=1, PricePerDay=2200, TotalUnits=6, AvailableUnits=5, ImageUrl="/images/sup.jpg", Tags="вода,спорт" },
       new Equipment { Id=5, Name="Газовая горелка Primus", Description="Компактная горелка для кемпинга", CategoryId=2, PricePerDay=500, TotalUnits=15, AvailableUnits=14, ImageUrl="/images/stove.jpg", Tags="туризм,кемпинг" },
       new Equipment { Id=6, Name="Лестница-трансформер", Description="Алюминиевая лестница 4×4 секции", CategoryId=3, PricePerDay=700, TotalUnits=4, AvailableUnits=3, ImageUrl="/images/ladder.jpg", Tags="стройка,дом" }
      ],
      Bookings = [new Booking { Id=1, EquipmentId=1, UserId=1, StartDate=DateTime.Today.AddDays(-2), EndDate=DateTime.Today.AddDays(2), TotalPrice=7500, Status=BookingStatus.Issued }, new Booking { Id=2, EquipmentId=2, UserId=1, StartDate=DateTime.Today.AddDays(12), EndDate=DateTime.Today.AddDays(14), TotalPrice=3600, Status=BookingStatus.Booked }]
    };
}







