namespace EquipmentRental.Models;

public enum BookingStatus { Booked, Issued, Returned, Overdue, Cancelled, Confirmed }
public enum EquipmentState { Available, Reserved, InUse, Repair, WrittenOff }
public enum UserRole { Client, Manager, Admin }

public class User { public int Id { get; set; } public string Name { get; set; } = ""; public string Email { get; set; } = ""; public string Password { get; set; } = ""; public UserRole Role { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
public class Category { public int Id { get; set; } public string Name { get; set; } = ""; public string Icon { get; set; } = ""; }
public class Equipment { public int Id { get; set; } public string Name { get; set; } = ""; public string Description { get; set; } = ""; public int CategoryId { get; set; } public decimal PricePerDay { get; set; } public int TotalUnits { get; set; } public int AvailableUnits { get; set; } public int RepairUnits { get; set; } public int WrittenOffUnits { get; set; } public EquipmentState State { get; set; } = EquipmentState.Available; public string ImageUrl { get; set; } = ""; public string Tags { get; set; } = ""; }
public class Booking { public int Id { get; set; } public int EquipmentId { get; set; } public int UserId { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public decimal TotalPrice { get; set; } public decimal DailyRate { get; set; } public BookingStatus Status { get; set; } = BookingStatus.Booked; public string? ReturnCondition { get; set; } public decimal Fine { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
public class AppData { public List<User> Users { get; set; } = []; public List<Category> Categories { get; set; } = []; public List<Equipment> Equipment { get; set; } = []; public List<Booking> Bookings { get; set; } = []; }

