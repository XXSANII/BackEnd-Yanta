using System.Security.Claims;
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
        // 1. อ่าน User ID จาก JWT ของ Supabase
        var userIdValue = context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Fail();
            return;
        }

        // 2. หา HttpContext เพื่ออ่าน storeId จาก Route
        var httpContext = context.Resource as HttpContext;

        if (httpContext is null)
        {
            // รองรับกรณี Resource เป็น AuthorizationFilterContext ด้วย
            if (context.Resource is Microsoft.AspNetCore.Mvc.Filters
                .AuthorizationFilterContext filterContext)
            {
                httpContext = filterContext.HttpContext;
            }
        }

        if (httpContext is null)
        {
            context.Fail();
            return;
        }

        // Endpoint ต้องมี route parameter ชื่อ storeId
        var storeIdValue = httpContext.Request.RouteValues["storeId"]?.ToString();

        if (!Guid.TryParse(storeIdValue, out var storeId))
        {
            context.Fail();
            return;
        }

        try
        {
            using var connection = _db.CreateConnection();

            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM public.stores s
                    WHERE s.id = @StoreId
                      AND s.is_active = TRUE
                      AND (
                          -- เจ้าของร้านมีสิทธิ์จัดการร้านของตน
                          s.owner_id = @UserId

                          OR

                          -- สมาชิกต้อง Active และมี Permission ตาม Role
                          EXISTS (
                              SELECT 1
                              FROM public.store_members sm
                              JOIN public.roles r
                                  ON r.id = sm.role_id
                              JOIN public.role_permissions rp
                                  ON rp.role_id = r.id
                              JOIN public.permissions p
                                  ON p.id = rp.permission_id
                              WHERE sm.store_id = s.id
                                AND sm.user_id = @UserId
                                AND sm.is_active = TRUE
                                AND p.code = @Permission
                          )
                      )
                );
                """;

            var allowed = await connection.QuerySingleAsync<bool>(
                sql,
                new
                {
                    StoreId = storeId,
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
                "Permission check failed for user {UserId}, store {StoreId}, permission {Permission}",
                userId,
                storeId,
                requirement.Permission);

            context.Fail();
        }
    }
}