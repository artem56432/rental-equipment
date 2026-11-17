namespace EquipmentRental.Models;
public class Category : IEntity
{
    public int Id
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
}
