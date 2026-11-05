using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Products;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;

    public ProductsController(ProductService service)
    {
        _service = service;
    }

    // ลูกค้า: เฉพาะเมนูที่พร้อมขาย
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailable([FromQuery] Guid? categoryId)
        => Ok(await _service.GetProductsAsync(categoryId, includeUnavailable: false));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetProductAsync(id, includeUnavailable: false));

    // ร้าน: รวมเมนูที่ปิดขาย
    [HttpGet("all")]
    [HasPermission("menu.view")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? categoryId)
        => Ok(await _service.GetProductsAsync(categoryId, includeUnavailable: true));

    [HttpPost]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Create(ProductRequest request)
    {
        var product = await _service.CreateProductAsync(request);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Update(Guid id, ProductRequest request)
        => Ok(await _service.UpdateProductAsync(id, request));

    [HttpPatch("{id:guid}/availability")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> SetAvailability(Guid id, AvailabilityRequest request)
        => Ok(await _service.SetAvailabilityAsync(id, request.IsAvailable));

    // เมนูที่เคยถูกสั่งแล้วลบไม่ได้ (409) ให้ปิดขายแทน
    [HttpDelete("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteProductAsync(id);
        return NoContent();
    }
}
