using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Orders;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

/// <summary>ฝั่งร้าน: รับและจัดการออเดอร์</summary>
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _service;

    public OrdersController(OrderService service)
    {
        _service = service;
    }

    // ค่าเริ่มต้น: pending + complete เรียงเก่า -> ใหม่
    // since: เฉพาะออเดอร์ที่สร้างตั้งแต่เวลานี้ (ISO 8601) ใช้จำกัดรายการ paid ของวันนี้
    [HttpGet]
    [HasPermission("order.view")]
    public async Task<IActionResult> GetAll([FromQuery] string[]? status, [FromQuery] DateTimeOffset? since)
        => Ok(await _service.GetByStatusAsync(status, since?.UtcDateTime));

    [HttpGet("{id:guid}")]
    [HasPermission("order.view")]
    public async Task<IActionResult> Get(Guid id)
        => Ok(await _service.GetAsync(id));

    // pending -> complete หรือ cancelled
    [HttpPatch("{id:guid}/status")]
    [HasPermission("order.update_status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
        => Ok(await _service.UpdateStatusAsync(id, request.Status));

    // complete -> paid
    [HttpPost("{id:guid}/pay")]
    [HasPermission("payment.confirm")]
    public async Task<IActionResult> Pay(Guid id)
        => Ok(await _service.PayAsync(id));

    [HttpPost("{id:guid}/items")]
    [HasPermission("order.update_status")]
    public async Task<IActionResult> AddItem(Guid id, OrderLineRequest request)
        => Ok(await _service.AddItemAsync(id, request));

    // แก้จำนวน หรือ status = cancelled เพื่อยกเลิกรายการ
    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    [HasPermission("order.update_status")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, UpdateOrderItemRequest request)
        => Ok(await _service.UpdateItemAsync(id, itemId, request));
}
