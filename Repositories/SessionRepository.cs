using Dapper;
using Npgsql;
using YantaBackEnd.Data;
using YantaBackEnd.Models.Sessions;

namespace YantaBackEnd.Repositories;

public sealed class SessionRepository
{
    private const string TableColumns =
        "id, table_number, qr_code_token, is_active, created_at, updated_at";

    private const string SessionSelect = """
        SELECT ts.id, ts.table_id, dt.table_number, ts.status,
               ts.opened_at, ts.closed_at, ts.bill_requested_at,
               COALESCE(SUM(o.total_amount) FILTER (WHERE o.status IN ('pending', 'complete', 'paid')), 0) AS total_amount,
               COALESCE(SUM(o.total_amount) FILTER (WHERE o.status IN ('pending', 'complete')), 0) AS unpaid_amount
        FROM public.table_sessions ts
        JOIN public.dining_tables dt ON dt.id = ts.table_id
        LEFT JOIN public.orders o ON o.session_id = ts.id
        """;

    private const string SessionGroup = "GROUP BY ts.id, dt.table_number";

    private readonly DapperContext _db;

    public SessionRepository(DapperContext db)
    {
        _db = db;
    }

    // ---------- Tables ----------

    public async Task<IReadOnlyList<DiningTable>> GetTablesAsync()
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<DiningTable>(
            $"SELECT {TableColumns} FROM public.dining_tables ORDER BY table_number");
        return rows.AsList();
    }

    public async Task<DiningTable?> GetTableAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DiningTable>(
            $"SELECT {TableColumns} FROM public.dining_tables WHERE id = @id", new { id });
    }

    public async Task<DiningTable?> GetTableByQrTokenAsync(Guid qrToken)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DiningTable>(
            $"SELECT {TableColumns} FROM public.dining_tables WHERE qr_code_token = @qrToken",
            new { qrToken });
    }

    public async Task<DiningTable> CreateTableAsync(TableRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleAsync<DiningTable>(
            $"""
            INSERT INTO public.dining_tables (table_number, is_active)
            VALUES (@TableNumber, @IsActive)
            RETURNING {TableColumns}
            """, request);
    }

    public async Task<DiningTable?> UpdateTableAsync(Guid id, TableRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DiningTable>(
            $"""
            UPDATE public.dining_tables
            SET table_number = @TableNumber, is_active = @IsActive
            WHERE id = @id
            RETURNING {TableColumns}
            """, new { id, request.TableNumber, request.IsActive });
    }

    public async Task<bool> DeleteTableAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            "DELETE FROM public.dining_tables WHERE id = @id", new { id }) > 0;
    }

    // ---------- Sessions ----------

    public async Task<TableSession?> GetOpenByTableAsync(Guid tableId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TableSession>(
            $"{SessionSelect} WHERE ts.table_id = @tableId AND ts.status IN ('active', 'closing') {SessionGroup}",
            new { tableId });
    }

    public async Task<TableSession?> GetAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TableSession>(
            $"{SessionSelect} WHERE ts.id = @id {SessionGroup}", new { id });
    }

    public async Task<IReadOnlyList<TableSession>> GetByStatusAsync(string[] statuses)
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<TableSession>(
            $"""
            {SessionSelect}
            WHERE ts.status = ANY(@statuses)
            {SessionGroup}
            ORDER BY COALESCE(ts.bill_requested_at, ts.opened_at)
            """, new { statuses });
        return rows.AsList();
    }

    /// <summary>
    /// คืน session ที่เปิดอยู่ของโต๊ะ ถ้าไม่มีให้สร้างใหม่
    /// (unique index จำกัดไว้ที่ 1 session เปิดต่อโต๊ะ)
    /// </summary>
    public async Task<Guid> OpenOrGetAsync(Guid tableId, Func<Guid, string> hashFor)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await GetOpenByTableAsync(tableId);

            if (existing is not null)
            {
                return existing.Id;
            }

            var id = Guid.NewGuid();

            try
            {
                using var connection = _db.CreateConnection();
                await connection.ExecuteAsync(
                    """
                    INSERT INTO public.table_sessions (id, table_id, session_token_hash)
                    VALUES (@id, @tableId, @hash)
                    """, new { id, tableId, hash = hashFor(id) });

                return id;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // มีคนสแกนพร้อมกันและสร้างไปแล้ว: วนไปอ่านอันที่มีอยู่
            }
        }

        throw new InvalidOperationException("Could not open or load table session.");
    }

    public async Task<bool> RequestBillAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            """
            UPDATE public.table_sessions
            SET status = 'closing', bill_requested_at = COALESCE(bill_requested_at, now())
            WHERE id = @id AND status IN ('active', 'closing')
            """, new { id }) > 0;
    }

    /// <summary>จ่ายทุกออเดอร์ที่ complete ของ session; ทำไม่ได้ถ้ายังมี pending (trigger จะปิด session เอง)</summary>
    public async Task<int> PayAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            """
            UPDATE public.orders SET status = 'paid'
            WHERE session_id = @id
              AND status = 'complete'
              AND NOT EXISTS (
                  SELECT 1 FROM public.orders p
                  WHERE p.session_id = @id AND p.status = 'pending')
            """, new { id });
    }

    /// <summary>ปิด session ได้เมื่อไม่มีออเดอร์ค้างชำระ</summary>
    public async Task<bool> CloseAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            """
            UPDATE public.table_sessions
            SET status = 'closed', closed_at = now()
            WHERE id = @id
              AND status IN ('active', 'closing')
              AND NOT EXISTS (
                  SELECT 1 FROM public.orders o
                  WHERE o.session_id = @id AND o.status IN ('pending', 'complete'))
            """, new { id }) > 0;
    }

    /// <summary>บังคับปิด (admin): ยกเลิกออเดอร์ที่ค้างอยู่ทั้งหมดแล้วปิด session</summary>
    public async Task<bool> ForceCloseAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            UPDATE public.orders SET status = 'cancelled'
            WHERE session_id = @id AND status IN ('pending', 'complete')
            """, new { id }, tx);

        var closed = await connection.ExecuteAsync(
            """
            UPDATE public.table_sessions
            SET status = 'closed', closed_at = now()
            WHERE id = @id AND status IN ('active', 'closing')
            """, new { id }, tx) > 0;

        tx.Commit();
        return closed;
    }

    public async Task<string?> GetRestaurantNameAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT name FROM public.restaurant_settings WHERE id = 1");
    }
}
