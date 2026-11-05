using Dapper;
using Microsoft.AspNetCore.Authorization;
using YantaBackEnd.Data;

namespace YantaBackEnd.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly DapperContext _db;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        DapperContext db,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // อ่าน User ID จาก JWT ของ Supabase
        var userIdValue = context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Fail();
            return;
        }

        try
        {
            using var connection = _db.CreateConnection();

            // ร้านเดียว: admin ผ่านทุกสิทธิ์
            // role อื่นใช้สิทธิ์ของ role และ member_permissions override รายคน
            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM public.store_members sm
                    JOIN public.roles r
                        ON r.id = sm.role_id
                    LEFT JOIN public.permissions p
                        ON p.code = @Permission
                    LEFT JOIN public.member_permissions mp
                        ON mp.store_member_id = sm.id
                       AND mp.permission_id = p.id
                    LEFT JOIN public.role_permissions rp
                        ON rp.role_id = r.id
                       AND rp.permission_id = p.id
                    WHERE sm.user_id = @UserId
                      AND sm.is_active = TRUE
                      AND (
                          r.name = 'admin'
                          OR (
                              p.id IS NOT NULL
                              AND COALESCE(mp.is_allowed, rp.permission_id IS NOT NULL)
                          )
                      )
                );
                """;

            var allowed = await connection.QuerySingleAsync<bool>(
                sql,
                new
                {
                    UserId = userId,
                    Permission = requirement.Permission
                });

            if (allowed)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            // Fail closed: หากฐานข้อมูลมีปัญหา ต้องไม่ให้ผ่านสิทธิ์
            _logger.LogError(
                ex,
                "Permission check failed for user {UserId}, permission {Permission}",
                userId,
                requirement.Permission);

            context.Fail();
        }
    }
}
