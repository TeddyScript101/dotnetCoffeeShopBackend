namespace CoffeeShopApi.Models;

public class Equipment : Product
{
    public string Brand { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty; // Grinder, Filter, Machine
}
