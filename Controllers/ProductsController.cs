using CoffeeShopApi.Data;
using CoffeeShopApi.DTOs;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly CoffeeShopDbContext _context;

    public ProductsController(CoffeeShopDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        var products = await _context.Products
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                SKU = p.SKU,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                ProductType = p is CoffeeBean ? ProductType.CoffeeBean : p is Equipment ? ProductType.Equipment : ProductType.Unknown
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("coffeebeans")]
    public async Task<ActionResult<IEnumerable<CoffeeBeanDto>>> GetCoffeeBeans()
    {
        var beans = await _context.CoffeeBeans
            .Include(cb => cb.Origin)
            .Select(cb => new CoffeeBeanDto
            {
                Id = cb.Id,
                Name = cb.Name,
                SKU = cb.SKU,
                Price = cb.Price,
                StockQuantity = cb.StockQuantity,
                ImageUrl = cb.ImageUrl,
                ProductType = ProductType.CoffeeBean,
                RoastLevel = cb.RoastLevel,
                TastingNotes = cb.TastingNotes,
                OriginCountry = cb.Origin != null ? cb.Origin.Country : string.Empty,
                OriginRegion = cb.Origin != null ? cb.Origin.Region : string.Empty
            })
            .ToListAsync();

        return Ok(beans);
    }

    [HttpGet("equipments")]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetEquipments()
    {
        var equipments = await _context.Equipments
            .Select(e => new EquipmentDto
            {
                Id = e.Id,
                Name = e.Name,
                SKU = e.SKU,
                Price = e.Price,
                StockQuantity = e.StockQuantity,
                ImageUrl = e.ImageUrl,
                ProductType = ProductType.Equipment,
                Brand = e.Brand,
                EquipmentType = e.EquipmentType
            })
            .ToListAsync();

        return Ok(equipments);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product == null)
        {
            return NotFound();
        }

        if (product is CoffeeBean bean)
        {
            await _context.Entry(bean).Reference(b => b.Origin).LoadAsync();
            return Ok(new CoffeeBeanDto
            {
                Id = bean.Id,
                Name = bean.Name,
                SKU = bean.SKU,
                Price = bean.Price,
                StockQuantity = bean.StockQuantity,
                ImageUrl = bean.ImageUrl,
                ProductType = ProductType.CoffeeBean,
                RoastLevel = bean.RoastLevel,
                TastingNotes = bean.TastingNotes,
                OriginCountry = bean.Origin != null ? bean.Origin.Country : string.Empty,
                OriginRegion = bean.Origin != null ? bean.Origin.Region : string.Empty
            });
        }
        else if (product is Equipment equipment)
        {
            return Ok(new EquipmentDto
            {
                Id = equipment.Id,
                Name = equipment.Name,
                SKU = equipment.SKU,
                Price = equipment.Price,
                StockQuantity = equipment.StockQuantity,
                ImageUrl = equipment.ImageUrl,
                ProductType = ProductType.Equipment,
                Brand = equipment.Brand,
                EquipmentType = equipment.EquipmentType
            });
        }

        return Ok(new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            SKU = product.SKU,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ImageUrl = product.ImageUrl,
            ProductType = ProductType.Unknown
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("coffeebeans")]
    public async Task<ActionResult<ProductDto>> CreateCoffeeBean(CreateCoffeeBeanRequest req)
    {
        var origin = await _context.Origins.FindAsync(req.OriginId);
        if (origin == null) return BadRequest("Origin not found");

        var bean = new CoffeeBean
        {
            Name = req.Name,
            SKU = req.SKU,
            Price = req.Price,
            StockQuantity = req.StockQuantity,
            ImageUrl = req.ImageUrl,
            RoastLevel = req.RoastLevel,
            TastingNotes = req.TastingNotes,
            OriginId = req.OriginId
        };

        _context.CoffeeBeans.Add(bean);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduct), new { id = bean.Id }, new { Id = bean.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("coffeebeans/{id}")]
    public async Task<IActionResult> UpdateCoffeeBean(Guid id, UpdateCoffeeBeanRequest req)
    {
        var bean = await _context.CoffeeBeans.FindAsync(id);
        if (bean == null) return NotFound();

        var origin = await _context.Origins.FindAsync(req.OriginId);
        if (origin == null) return BadRequest("Origin not found");

        bean.Name = req.Name;
        bean.SKU = req.SKU;
        bean.Price = req.Price;
        bean.StockQuantity = req.StockQuantity;
        bean.ImageUrl = req.ImageUrl;
        bean.RoastLevel = req.RoastLevel;
        bean.TastingNotes = req.TastingNotes;
        bean.OriginId = req.OriginId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("equipments")]
    public async Task<ActionResult<ProductDto>> CreateEquipment(CreateEquipmentRequest req)
    {
        var equipment = new Equipment
        {
            Name = req.Name,
            SKU = req.SKU,
            Price = req.Price,
            StockQuantity = req.StockQuantity,
            ImageUrl = req.ImageUrl,
            Brand = req.Brand,
            EquipmentType = req.EquipmentType
        };

        _context.Equipments.Add(equipment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduct), new { id = equipment.Id }, new { Id = equipment.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("equipments/{id}")]
    public async Task<IActionResult> UpdateEquipment(Guid id, UpdateEquipmentRequest req)
    {
        var equipment = await _context.Equipments.FindAsync(id);
        if (equipment == null) return NotFound();

        equipment.Name = req.Name;
        equipment.SKU = req.SKU;
        equipment.Price = req.Price;
        equipment.StockQuantity = req.StockQuantity;
        equipment.ImageUrl = req.ImageUrl;
        equipment.Brand = req.Brand;
        equipment.EquipmentType = req.EquipmentType;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
