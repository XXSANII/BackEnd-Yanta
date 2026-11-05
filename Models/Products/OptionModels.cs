using System.ComponentModel.DataAnnotations;

namespace YantaBackEnd.Models.Products;

public static class OptionGroupKind
{
    public const string Addon = "addon";
    public const string Remove = "remove";
    public const string Single = "single";

    public static readonly string[] All = [Addon, Remove, Single];
}

public sealed class OptionGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = OptionGroupKind.Addon;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public List<ProductOption> Options { get; set; } = [];
}

public sealed class ProductOption
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsAvailable { get; set; }
    public int SortOrder { get; set; }
}

public sealed class OptionRequest
{
    /// <summary>มี = แก้ตัวเลือกเดิม, ไม่มี = เพิ่มใหม่</summary>
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000)]
    public decimal PriceDelta { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed class OptionGroupRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Kind { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    [Required, MinLength(1)]
    public List<OptionRequest> Options { get; set; } = [];
}
