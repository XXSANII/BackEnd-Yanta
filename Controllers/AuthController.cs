using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Data;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("test")]
    [Authorize]
    public IActionResult TestAuthentication()
    {
        return Ok(new
        {
            authenticated = true,
            userId = User.FindFirst("sub")?.Value,
            message = "JWT validation successful"
        });
    }

    /// <summary>
    /// ฝั่งร้านเรียกหลัง login: ยืนยันว่าเป็นพนักงานของร้านจริง
    /// และได้ role + สิทธิ์ไปซ่อน/แสดงปุ่มในหน้าเว็บ (403 ถ้าไม่ใช่พนักงาน)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me([FromServices] DapperContext db)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
        {
            return Unauthorized();
        }

        using var connection = db.CreateConnection();

        var member = await connection.QuerySingleOrDefaultAsync<MemberRow>(
            """
            SELECT sm.id, sm.role_id, r.name AS role_name
            FROM public.store_members sm
            JOIN public.roles r ON r.id = sm.role_id
            WHERE sm.user_id = @userId AND sm.is_active
            """, new { userId });

        if (member is null)
        {
            return Forbid();
        }

        var isAdmin = member.RoleName == "admin";

        // admin ได้ทุกสิทธิ์; role อื่นใช้สิทธิ์ของ role และ override รายคน
        var permissions = await connection.QueryAsync<string>(
            """
            SELECT p.code
            FROM public.permissions p
            LEFT JOIN public.member_permissions mp
                   ON mp.permission_id = p.id AND mp.store_member_id = @MemberId
            LEFT JOIN public.role_permissions rp
                   ON rp.permission_id = p.id AND rp.role_id = @RoleId
            WHERE @IsAdmin OR COALESCE(mp.is_allowed, rp.permission_id IS NOT NULL)
            ORDER BY p.code
            """, new { MemberId = member.Id, member.RoleId, IsAdmin = isAdmin });

        return Ok(new
        {
            userId,
            role = member.RoleName,
            isAdmin,
            permissions
        });
    }

    private sealed class MemberRow
    {
        public Guid Id { get; set; }
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }
}
