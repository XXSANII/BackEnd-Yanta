using System.ComponentModel.DataAnnotations;

namespace YantaBackEnd.Models.Products;

public sealed class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OptionGroup> OptionGroups { get; set; } = [];
}

public sealed class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>ลำดับแท็บในหน้าลูกค้า (น้อยไปมาก)</summary>
    public int SortOrder { get; set; }
}

public sealed class ProductRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [StringLength(2000)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>null = ไม่แตะการผูกชุดตัวเลือก; อาร์เรย์ = แทนที่ทั้งชุด</summary>
    public Guid[]? OptionGroupIds { get; set; }
}

public sealed class AvailabilityRequest
{
    public bool IsAvailable { get; set; }
}
