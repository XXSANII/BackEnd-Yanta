using Dapper;
using YantaBackEnd.Data;
using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Orders;
using YantaBackEnd.Models.Sessions;

namespace YantaBackEnd.Repositories;

public sealed class OrderRepository
{
    private const string OrderSelect = """
        SELECT o.id, o.order_number, o.session_id, dt.table_number, o.status,
               o.total_amount, o.note, o.created_at, o.updated_at
        FROM public.orders o
        LEFT JOIN public.table_sessions ts ON ts.id = o.session_id
        LEFT JOIN public.dining_tables dt ON dt.id = ts.table_id
        """;

    private const string ItemColumns =
        "id, order_id, product_id, product_name, unit_price, quantity, subtotal, status, created_at, options::text AS options_json, note";

    private readonly DapperContext _db;

    public OrderRepository(DapperContext db)
    {
        _db = db;
    }

    /// <summary>ออเดอร์ฝั่งร้าน เรียงเก่า -> ใหม่</summary>
    public async Task<IReadOnlyList<Order>> GetByStatusAsync(string[] statuses, DateTime? since)
    {
        using var connection = _db.CreateConnection();
        var orders = (await connection.QueryAsync<Order>(
            $"""
            {OrderSelect}
            WHERE o.status = ANY(@statuses)
              AND (@since::timestamptz IS NULL OR o.created_at >= @since::timestamptz)
            ORDER BY o.created_at, o.order_number
            """,
            new { statuses, since })).AsList();

        await AttachItemsAsync(connection, orders);
        return orders;
    }

    public async Task<IReadOnlyList<Order>> GetBySessionAsync(Guid sessionId)
    {
        using var connection = _db.CreateConnection();
        var orders = (await connection.QueryAsync<Order>(
            $"{OrderSelect} WHERE o.session_id = @sessionId ORDER BY o.created_at, o.order_number",
            new { sessionId })).AsList();

        await AttachItemsAsync(connection, orders);
        return orders;
    }

    public async Task<Order?> GetAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        var order = await connection.QuerySingleOrDefaultAsync<Order>(
            $"{OrderSelect} WHERE o.id = @id", new { id });

        if (order is not null)
        {
            await AttachItemsAsync(connection, [order]);
        }

        return order;
    }

    public async Task<Guid> CreateAsync(Guid sessionId, string? note, IReadOnlyList<NewOrderLine> lines)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        // ล็อก session กันสั่งซ้อนกับตอนปิด/ชำระ
        var status = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT status FROM public.table_sessions WHERE id = @sessionId FOR UPDATE",
            new { sessionId }, tx);

        if (status != SessionStatus.Active)
        {
            throw AppException.Conflict(
                status == SessionStatus.Closing
                    ? "ขอเช็คบิลแล้ว ไม่สามารถสั่งอาหารเพิ่มได้"
                    : "โต๊ะนี้ปิดแล้ว");
        }

        var orderId = await connection.QuerySingleAsync<Guid>(
            "INSERT INTO public.orders (session_id, note) VALUES (@sessionId, @note) RETURNING id",
            new { sessionId, note }, tx);

        // ยอดรวมถูกคำนวณโดย trigger trg_order_items_recalc
        await connection.ExecuteAsync(
            """
            INSERT INTO public.order_items (order_id, product_id, product_name, unit_price, quantity, options, note)
            VALUES (@orderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @OptionsJson::jsonb, @Note)
            """,
            lines.Select(l => new { orderId, l.ProductId, l.ProductName, l.UnitPrice, l.Quantity, l.OptionsJson, l.Note }),
            tx);

        tx.Commit();
        return orderId;
    }

    /// <summary>เปลี่ยนสถานะแบบกัน race: สำเร็จเฉพาะเมื่อสถานะปัจจุบันตรงกับที่คาด</summary>
    public async Task<bool> TransitionAsync(Guid id, string from, string to)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            "UPDATE public.orders SET status = @to WHERE id = @id AND status = @from",
            new { id, from, to }) > 0;
    }

    /// <summary>แก้จำนวน/ยกเลิกรายการ ได้เฉพาะออเดอร์ที่ยังไม่ชำระ/ไม่ถูกยกเลิก</summary>
    public async Task<bool> UpdateItemAsync(Guid orderId, Guid itemId, int? quantity, string? status)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            """
            UPDATE public.order_items
            SET quantity = COALESCE(@quantity, quantity),
                status = COALESCE(@status, status)
            WHERE id = @itemId
              AND order_id = @orderId
              AND EXISTS (
                  SELECT 1 FROM public.orders o
                  WHERE o.id = @orderId AND o.status IN ('pending', 'complete'))
            """, new { orderId, itemId, quantity, status }) > 0;
    }

    public async Task<bool> AddItemAsync(Guid orderId, NewOrderLine line)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            """
            INSERT INTO public.order_items (order_id, product_id, product_name, unit_price, quantity, options, note)
            SELECT @orderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @OptionsJson::jsonb, @Note
            WHERE EXISTS (
                SELECT 1 FROM public.orders o
                WHERE o.id = @orderId AND o.status IN ('pending', 'complete'))
            """,
            new { orderId, line.ProductId, line.ProductName, line.UnitPrice, line.Quantity, line.OptionsJson, line.Note }) > 0;
    }

    /// <summary>ถ้ายกเลิกทุกรายการแล้ว ให้ยกเลิกทั้งออเดอร์</summary>
    public async Task CancelIfNoActiveItemsAsync(Guid orderId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            """
            UPDATE public.orders SET status = 'cancelled'
            WHERE id = @orderId
              AND status IN ('pending', 'complete')
              AND NOT EXISTS (
                  SELECT 1 FROM public.order_items
                  WHERE order_id = @orderId AND status = 'active')
            """, new { orderId });
    }

    private static async Task AttachItemsAsync(System.Data.IDbConnection connection, IReadOnlyList<Order> orders)
    {
        if (orders.Count == 0)
        {
            return;
        }

        var ids = orders.Select(o => o.Id).ToArray();

        var items = await connection.QueryAsync<OrderItem>(
            $"SELECT {ItemColumns} FROM public.order_items WHERE order_id = ANY(@ids) ORDER BY created_at, id",
            new { ids });

        var byOrder = items.ToLookup(i => i.OrderId);

        foreach (var order in orders)
        {
            order.Items = byOrder[order.Id].ToList();
        }
    }
}
