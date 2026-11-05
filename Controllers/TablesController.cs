using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Sessions;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly SessionService _service;

    public TablesController(SessionService service)
    {
        _service = service;
    }

    // qrCodeToken คือค่าที่ฝังใน QR ของโต๊ะ (frontend นำไปสร้างรูป QR)
    [HttpGet]
    [HasPermission("table.view")]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetTablesAsync());

    [HttpGet("{id:guid}")]
    [HasPermission("table.view")]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetTableAsync(id));

    [HttpPost]
    [HasPermission("table.manage")]
    public async Task<IActionResult> Create(TableRequest request)
    {
        var table = await _service.CreateTableAsync(request);
        return CreatedAtAction(nameof(Get), new { id = table.Id }, table);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("table.manage")]
    public async Task<IActionResult> Update(Guid id, TableRequest request)
        => Ok(await _service.UpdateTableAsync(id, request));

    // โต๊ะที่เคยมี session แล้วลบไม่ได้ (409) ให้ปิดใช้งานแทน
    [HttpDelete("{id:guid}")]
    [HasPermission("table.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteTableAsync(id);
        return NoContent();
    }
}
