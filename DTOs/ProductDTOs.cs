using System;

namespace CoffeeShopApi.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public Models.ProductType ProductType { get; set; }
}

public class CoffeeBeanDto : ProductDto
{
    public string RoastLevel { get; set; } = string.Empty;
    public string TastingNotes { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
    public string OriginRegion { get; set; } = string.Empty;
}

public class EquipmentDto : ProductDto
{
    public string Brand { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
}

public class CreateCoffeeBeanRequest
{
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    
    public string RoastLevel { get; set; } = string.Empty;
    public string TastingNotes { get; set; } = string.Empty;
    public Guid OriginId { get; set; }
}

public class UpdateCoffeeBeanRequest : CreateCoffeeBeanRequest
{
}

public class CreateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    
    public string Brand { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
}

public class UpdateEquipmentRequest : CreateEquipmentRequest
{
}
