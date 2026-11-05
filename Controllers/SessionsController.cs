using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

/// <summary>ฝั่งร้าน: ดู/ปิด/ชำระเงินของแต่ละโต๊ะ</summary>
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    // สิทธิ์ที่ไม่มีใน role ใดเลย มีแต่ admin ที่ผ่าน (admin ผ่านทุกสิทธิ์)
    private const string AdminOverride = "admin.override";

    private readonly SessionService _service;
    private readonly IAuthorizationService _authorization;

    public SessionsController(SessionService service, IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
    }

    // ค่าเริ่มต้น: โต๊ะที่เปิดอยู่ (active, closing) เรียงตามเวลาที่ขอเช็คบิล/เปิดโต๊ะ
    [HttpGet]
    [HasPermission("session.view")]
    public async Task<IActionResult> GetAll([FromQuery] string[]? status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("{id:guid}")]
    [HasPermission("session.view")]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetAsync(id));

    // ชำระเงินทั้งโต๊ะ (ออเดอร์ complete -> paid) แล้วปิดโต๊ะอัตโนมัติ
    [HttpPost("{id:guid}/pay")]
    [HasPermission("payment.confirm")]
    public async Task<IActionResult> Pay(Guid id)
        => Ok(await _service.PayAsync(id));

    // ปิดโต๊ะปกติ: ต้องไม่มีออเดอร์ค้างชำระ
    // force=true: เฉพาะ admin ยกเลิกออเดอร์ที่ค้างอยู่แล้วปิดทันที
    [HttpPost("{id:guid}/close")]
    [HasPermission("session.close")]
    public async Task<IActionResult> Close(Guid id, [FromQuery] bool force = false)
    {
        if (!force)
        {
            return Ok(await _service.CloseAsync(id));
        }

        var admin = await _authorization.AuthorizeAsync(
            User, null, HasPermissionAttribute.PolicyPrefix + AdminOverride);

        return admin.Succeeded
            ? Ok(await _service.ForceCloseAsync(id))
            : Forbid();
    }
}
