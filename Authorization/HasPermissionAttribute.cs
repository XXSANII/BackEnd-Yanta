using Microsoft.AspNetCore.Authorization;

namespace YantaBackEnd.Authorization;

/// <summary>
/// ใช้แทน [Authorize(Policy = "Permission:menu.view")]
/// ตัวอย่าง: [HasPermission("menu.view")]
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permission)
    {
        Policy = PolicyPrefix + permission;
    }
}