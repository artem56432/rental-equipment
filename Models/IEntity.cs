namespace EquipmentRental.Models;
// Общий идентификатор для объектов, которые сохраняются в базе данных.
public interface IEntity
{
    int Id
    {
        get; set;
    }
}
