namespace CoffeeShopApi.Models;

public class Origin
{
    public Guid Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    
    // Navigation
    public ICollection<CoffeeBean> CoffeeBeans { get; set; } = new List<CoffeeBean>();
}
