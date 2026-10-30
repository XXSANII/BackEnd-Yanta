using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace YantaBackEnd.Authorization;

/// <summary>
/// สร้าง Policy ชื่อ "Permission:xxx" แบบ dynamic
/// ไม่ต้องลงทะเบียนรายการ permission ใน Program.cs อีกต่อไป
/// ชื่อ permission ที่ไม่มีใน DB จะไม่มีใครผ่านสิทธิ์ (handler คืน false)
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(
                HasPermissionAttribute.PolicyPrefix,
                StringComparison.Ordinal))
        {
            var permission =
                policyName[HasPermissionAttribute.PolicyPrefix.Length..];

            if (!string.IsNullOrWhiteSpace(permission))
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}