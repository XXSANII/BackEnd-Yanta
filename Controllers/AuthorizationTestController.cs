using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/stores/{storeId:guid}/authorization-test")]
public class AuthorizationTestController : ControllerBase
{
    // GET: ทดสอบสิทธิ์ดูเมนู
    [HttpGet("menu")]
    [HasPermission("menu.view")]
    public IActionResult TestMenuPermission(Guid storeId)
    {
        return Ok(new
        {
            success = true,
            storeId,
            userId = User.FindFirst("sub")?.Value,
            permission = "menu.view",
            message = "You can view the menu."
        });
    }

    // POST: ทดสอบสิทธิ์จัดการเมนู
    [HttpPost("menu")]
    [HasPermission("menu.manage")]
    public IActionResult TestManageMenu(Guid storeId)
    {
        return Ok(new
        {
            success = true,
            storeId,
            userId = User.FindFirst("sub")?.Value,
            permission = "menu.manage",
            message = "You can manage the menu."
        });
    }
}