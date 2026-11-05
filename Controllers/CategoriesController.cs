using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Products;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ProductService _service;

    public CategoriesController(ProductService service)
    {
        _service = service;
    }

    // ลูกค้าดูได้โดยไม่ต้อง login
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetCategoriesAsync());

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetCategoryAsync(id));

    [HttpPost]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        var category = await _service.CreateCategoryAsync(request);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Update(Guid id, CategoryRequest request)
        => Ok(await _service.UpdateCategoryAsync(id, request));

    [HttpDelete("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteCategoryAsync(id);
        return NoContent();
    }
}
