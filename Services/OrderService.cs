using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Orders;
using YantaBackEnd.Repositories;

namespace YantaBackEnd.Services;

public sealed class OrderService
{
    private readonly OrderRepository _orders;
    private readonly ProductRepository _products;

    public OrderService(OrderRepository orders, ProductRepository products)
    {
        _orders = orders;
        _products = products;
    }

    // ---------- ฝั่งลูกค้า ----------

    public Task<IReadOnlyList<Order>> GetForSessionAsync(Guid sessionId)
        => _orders.GetBySessionAsync(sessionId);

    public async Task<Order> CreateForSessionAsync(Guid sessionId, CreateOrderRequest request)
    {
        var lines = await BuildLinesAsync(request.Items);
        var orderId = await _orders.CreateAsync(sessionId, request.Note, lines);
        return await GetAsync(orderId);
    }

    // ---------- ฝั่งร้าน ----------

    public Task<IReadOnlyList<Order>> GetByStatusAsync(string[]? statuses, DateTime? since = null)
    {
        var filter = statuses is { Length: > 0 } ? statuses : OrderStatus.Active;

        if (filter.Any(s => !OrderStatus.All.Contains(s)))
        {
            throw AppException.BadRequest("สถานะไม่ถูกต้อง");
        }

        return _orders.GetByStatusAsync(filter, since);
    }

    public async Task<Order> GetAsync(Guid id)
        => await _orders.GetAsync(id) ?? throw AppException.NotFound("ไม่พบออเดอร์");

    /// <summary>pending -> complete หรือยกเลิกออเดอร์ (ชำระเงินใช้ PayAsync)</summary>
    public async Task<Order> UpdateStatusAsync(Guid id, string target)
    {
        if (target is not (OrderStatus.Complete or OrderStatus.Cancelled))
        {
            throw AppException.BadRequest("เปลี่ยนสถานะได้เฉพาะเป็น complete หรือ cancelled");
        }

        var order = await GetAsync(id);

        var allowed = target == OrderStatus.Complete
            ? order.Status == OrderStatus.Pending
            : order.Status is OrderStatus.Pending or OrderStatus.Complete;

        if (!allowed || !await _orders.TransitionAsync(id, order.Status, target))
        {
            throw AppException.Conflict($"ออเดอร์สถานะ {order.Status} เปลี่ยนเป็น {target} ไม่ได้");
        }

        return await GetAsync(id);
    }

    /// <summary>complete -> paid</summary>
    public async Task<Order> PayAsync(Guid id)
    {
        var order = await GetAsync(id);

        if (order.Status != OrderStatus.Complete
            || !await _orders.TransitionAsync(id, OrderStatus.Complete, OrderStatus.Paid))
        {
            throw AppException.Conflict("ชำระเงินได้เฉพาะออเดอร์ที่ทำเสร็จแล้ว (complete)");
        }

        return await GetAsync(id);
    }

    public async Task<Order> UpdateItemAsync(Guid orderId, Guid itemId, UpdateOrderItemRequest request)
    {
        if (request.Status is not (null or OrderItemStatus.Active or OrderItemStatus.Cancelled))
        {
            throw AppException.BadRequest("สถานะรายการไม่ถูกต้อง");
        }

        if (request.Quantity is null && request.Status is null)
        {
            throw AppException.BadRequest("ไม่มีข้อมูลที่จะแก้ไข");
        }

        var order = await GetAsync(orderId);

        if (order.Items.All(i => i.Id != itemId))
        {
            throw AppException.NotFound("ไม่พบรายการอาหารในออเดอร์นี้");
        }

        if (!await _orders.UpdateItemAsync(orderId, itemId, request.Quantity, request.Status))
        {
            throw AppException.Conflict("ออเดอร์ที่ชำระแล้วหรือถูกยกเลิกแล้วแก้ไขไม่ได้");
        }

        await _orders.CancelIfNoActiveItemsAsync(orderId);
        return await GetAsync(orderId);
    }

    public async Task<Order> AddItemAsync(Guid orderId, OrderLineRequest request)
    {
        await GetAsync(orderId);

        var line = (await BuildLinesAsync([request]))[0];

        if (!await _orders.AddItemAsync(orderId, line))
        {
            throw AppException.Conflict("ออเดอร์ที่ชำระแล้วหรือถูกยกเลิกแล้วแก้ไขไม่ได้");
        }

        return await GetAsync(orderId);
    }

    /// <summary>รวมรายการซ้ำ (เมนู + ชุดตัวเลือก + หมายเหตุเหมือนกัน) และสร้างบรรทัดพร้อมราคา ณ ตอนสั่ง</summary>
    private async Task<List<NewOrderLine>> BuildLinesAsync(IEnumerable<OrderLineRequest> items)
    {
        var merged = items
            .GroupBy(i => (
                i.ProductId,
                Options: string.Join(',', (i.OptionIds ?? []).OrderBy(id => id)),
                Note: NormalizeNote(i.Note)))
            .Select(g =>
            {
                var first = g.First();
                return new OrderLineRequest
                {
                    ProductId = first.ProductId,
                    OptionIds = first.OptionIds ?? [],
                    Note = first.Note,
                    Quantity = Math.Min(g.Sum(i => i.Quantity), 99),
                };
            })
            .ToList();

        var productIds = merged.Select(l => l.ProductId).Distinct().ToArray();
        var products = await _products.GetProductsByIdsAsync(productIds);
        var lines = new List<NewOrderLine>();

        foreach (var line in merged)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId);

            if (product is null)
            {
                throw AppException.BadRequest("มีเมนูที่ไม่พบในระบบ");
            }

            lines.Add(OrderLineBuilder.Build(product, line));
        }

        return lines;
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
