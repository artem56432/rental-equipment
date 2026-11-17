namespace EquipmentRental.Models;
public class Booking : IEntity
{
    public int Id
    {
        get; set;
    }
    public int EquipmentId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public DateTime StartDate
    {
        get; set;
    }
    public DateTime EndDate
    {
        get; set;
    }
    public decimal TotalPrice
    {
        get; set;
    }
    public decimal DailyRate
    {
        get; set;
    }
    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public string? ReturnCondition
    {
        get; set;
    }
    public decimal Fine
    {
        get; set;
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
