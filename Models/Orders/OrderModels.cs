using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YantaBackEnd.Models.Orders;

/// <summary>pending -> complete -> paid, หรือ cancelled</summary>
public static class OrderStatus
{
    public const string Pending = "pending";
    public const string Complete = "complete";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Pending, Complete, Paid, Cancelled];
    public static readonly string[] Active = [Pending, Complete];
}

public static class OrderItemStatus
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";
}

public sealed class Order
{
    public Guid Id { get; set; }
    public long OrderNumber { get; set; }
    public Guid? SessionId { get; set; }
    public string? TableNumber { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public string Status { get; set; } = OrderItemStatus.Active;
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }

    /// <summary>สำเนาตัวเลือก ณ ตอนสั่ง (อ่านจากคอลัมน์ options เป็น options_json)</summary>
    [JsonIgnore]
    public string OptionsJson
    {
        get => _optionsJson;
        set
        {
            _optionsJson = string.IsNullOrWhiteSpace(value) ? "[]" : value;
            Options = JsonSerializer.Deserialize<List<OrderItemOption>>(_optionsJson, OrderItemOption.JsonOptions) ?? [];
        }
    }

    public IReadOnlyList<OrderItemOption> Options { get; private set; } = [];

    private string _optionsJson = "[]";
}

public sealed record OrderItemOption(string GroupName, string Name, decimal PriceDelta)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public sealed class OrderLineRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; }

    /// <summary>ตัวเลือกที่เลือก (ไม่ส่งหรือส่ง null = ไม่เลือก)</summary>
    public Guid[]? OptionIds { get; set; } = [];

    [StringLength(200)]
    public string? Note { get; set; }
}

public sealed class CreateOrderRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public List<OrderLineRequest> Items { get; set; } = [];

    [StringLength(500)]
    public string? Note { get; set; }
}

public sealed class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public sealed class UpdateOrderItemRequest
{
    [Range(1, 99)]
    public int? Quantity { get; set; }

    /// <summary>active | cancelled</summary>
    public string? Status { get; set; }
}

/// <summary>แถวที่ต้องบันทึกตอนสร้างออเดอร์ (ชื่อ/ราคา ณ ตอนสั่ง)</summary>
public sealed record NewOrderLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, string OptionsJson, string? Note);
