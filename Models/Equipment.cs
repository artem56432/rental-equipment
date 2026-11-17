namespace EquipmentRental.Models;
public class Equipment : IEntity
{
    public int Id
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CategoryId
    {
        get; set;
    }
    public decimal PricePerDay
    {
        get; set;
    }
    public int TotalUnits
    {
        get; set;
    }
    public int AvailableUnits
    {
        get; set;
    }
    public int RepairUnits
    {
        get; set;
    }
    public int WrittenOffUnits
    {
        get; set;
    }
    public EquipmentState State { get; set; } = EquipmentState.Available;
    public string ImageUrl { get; set; } = "";
    public string Tags { get; set; } = "";
}
