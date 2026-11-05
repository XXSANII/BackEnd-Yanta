using System.ComponentModel.DataAnnotations;

namespace YantaBackEnd.Models.Sessions;

public static class SessionStatus
{
    public const string Active = "active";
    public const string Closing = "closing";
    public const string Closed = "closed";

    public static readonly string[] Open = [Active, Closing];
}

public sealed class DiningTable
{
    public Guid Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public Guid QrCodeToken { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TableSession
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Status { get; set; } = SessionStatus.Active;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? BillRequestedAt { get; set; }

    /// <summary>ยอดรวมออเดอร์ที่ไม่ถูกยกเลิก</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>ยอดที่ยังไม่ชำระ (pending + complete)</summary>
    public decimal UnpaidAmount { get; set; }
}

public sealed class ScanRequest
{
    [Required]
    public Guid QrToken { get; set; }
}

public sealed class ScanResponse
{
    public string SessionToken { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? RestaurantName { get; set; }
}

public sealed class TableRequest
{
    [Required, StringLength(20)]
    public string TableNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
