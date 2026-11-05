using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Products;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/option-groups")]
public class OptionGroupsController : ControllerBase
{
    private readonly OptionGroupService _service;

    public OptionGroupsController(OptionGroupService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("menu.view")]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [HasPermission("menu.view")]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetAsync(id));

    [HttpPost]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Create(OptionGroupRequest request)
    {
        var group = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Update(Guid id, OptionGroupRequest request)
        => Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [HasPermission("menu.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
