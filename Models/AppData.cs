namespace EquipmentRental.Models;
public class AppData
{
    public List<User> Users { get; set; } = [];
    public List<Category> Categories { get; set; } = [];
    public List<Equipment> Equipment { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
}
