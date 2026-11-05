using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YantaBackEnd.Authorization;
using YantaBackEnd.Models.Orders;
using YantaBackEnd.Models.Sessions;
using YantaBackEnd.Services;

namespace YantaBackEnd.Controllers;

/// <summary>
/// ฝั่งลูกค้า: สแกน QR แล้วส่ง token ใน header X-Session-Token
/// ดูเมนูใช้ GET /api/categories และ GET /api/products
/// </summary>
[ApiController]
[Route("api/customer")]
public class CustomerController : ControllerBase
{
    private const string Scheme = TableSessionDefaults.Scheme;

    private readonly SessionService _sessions;
    private readonly OrderService _orders;

    public CustomerController(SessionService sessions, OrderService orders)
    {
        _sessions = sessions;
        _orders = orders;
    }

    private Guid SessionId => Guid.Parse(User.FindFirst(TableSessionDefaults.SessionIdClaim)!.Value);

    // สแกน QR: ส่ง qrToken ของโต๊ะ ได้ sessionToken กลับไปใช้ในคำขอถัดไป
    [HttpPost("scan")]
    [AllowAnonymous]
    public async Task<IActionResult> Scan(ScanRequest request)
        => Ok(await _sessions.ScanAsync(request.QrToken));

    // ข้อมูลโต๊ะและสถานะ (เลขโต๊ะ, ขอเช็คบิลแล้วหรือยัง, ยอดรวม)
    [HttpGet("session")]
    [Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> GetSession()
        => Ok(await _sessions.GetAsync(SessionId));

    // ประวัติออเดอร์ของโต๊ะนี้ พร้อมสถานะ
    [HttpGet("orders")]
    [Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> GetOrders()
        => Ok(await _orders.GetForSessionAsync(SessionId));

    // ส่งออเดอร์เข้าครัว
    [HttpPost("orders")]
    [Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
        => Ok(await _orders.CreateForSessionAsync(SessionId, request));

    // กดเช็คบิล
    [HttpPost("bill-request")]
    [Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> RequestBill()
        => Ok(await _sessions.RequestBillAsync(SessionId));
}
