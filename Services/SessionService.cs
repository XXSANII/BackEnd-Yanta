using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Sessions;
using YantaBackEnd.Repositories;

namespace YantaBackEnd.Services;

public sealed class SessionService
{
    private readonly SessionRepository _repository;
    private readonly TableSessionTokenService _tokens;

    public SessionService(SessionRepository repository, TableSessionTokenService tokens)
    {
        _repository = repository;
        _tokens = tokens;
    }

    // ---------- ลูกค้า ----------

    /// <summary>สแกน QR: เปิด session ของโต๊ะ (หรือใช้ session เดิม) แล้วออก token</summary>
    public async Task<ScanResponse> ScanAsync(Guid qrToken)
    {
        var table = await _repository.GetTableByQrTokenAsync(qrToken);

        if (table is null || !table.IsActive)
        {
            throw AppException.NotFound("ไม่พบโต๊ะ หรือโต๊ะนี้ปิดใช้งาน");
        }

        var sessionId = await _repository.OpenOrGetAsync(
            table.Id, id => TableSessionTokenService.Hash(_tokens.Create(id)));

        return new ScanResponse
        {
            SessionId = sessionId,
            SessionToken = _tokens.Create(sessionId),
            TableNumber = table.TableNumber,
            RestaurantName = await _repository.GetRestaurantNameAsync()
        };
    }

    /// <summary>ใช้ตรวจ token ของลูกค้า: คืน session เฉพาะที่ยังเปิดอยู่</summary>
    public async Task<TableSession?> GetOpenAsync(Guid sessionId)
    {
        var session = await _repository.GetAsync(sessionId);
        return session is { Status: SessionStatus.Active or SessionStatus.Closing } ? session : null;
    }

    public async Task<TableSession> RequestBillAsync(Guid sessionId)
    {
        if (!await _repository.RequestBillAsync(sessionId))
        {
            throw AppException.Conflict("โต๊ะนี้ปิดแล้ว");
        }

        return await GetAsync(sessionId);
    }

    // ---------- ร้าน: session ----------

    public Task<IReadOnlyList<TableSession>> GetByStatusAsync(string[]? statuses)
    {
        var filter = statuses is { Length: > 0 } ? statuses : SessionStatus.Open;

        if (filter.Any(s => s is not (SessionStatus.Active or SessionStatus.Closing or SessionStatus.Closed)))
        {
            throw AppException.BadRequest("สถานะไม่ถูกต้อง");
        }

        return _repository.GetByStatusAsync(filter);
    }

    public async Task<TableSession> GetAsync(Guid id)
        => await _repository.GetAsync(id) ?? throw AppException.NotFound("ไม่พบ session");

    /// <summary>ชำระเงินทั้งโต๊ะ: ออเดอร์ complete -> paid แล้ว trigger ปิด session ให้</summary>
    public async Task<TableSession> PayAsync(Guid id)
    {
        var session = await GetAsync(id);

        if (session.Status == SessionStatus.Closed)
        {
            throw AppException.Conflict("โต๊ะนี้ปิดแล้ว");
        }

        if (await _repository.PayAsync(id) == 0)
        {
            throw AppException.Conflict("ไม่มีออเดอร์ที่ชำระได้ หรือยังมีออเดอร์ที่รอดำเนินการ (pending)");
        }

        return await GetAsync(id);
    }

    public async Task<TableSession> CloseAsync(Guid id)
    {
        await GetAsync(id);

        if (!await _repository.CloseAsync(id))
        {
            throw AppException.Conflict("ปิดไม่ได้: โต๊ะปิดแล้ว หรือยังมีออเดอร์ค้างชำระ");
        }

        return await GetAsync(id);
    }

    /// <summary>admin เท่านั้น: ปิดโต๊ะทันที ออเดอร์ที่ค้างอยู่จะถูกยกเลิก</summary>
    public async Task<TableSession> ForceCloseAsync(Guid id)
    {
        await GetAsync(id);

        if (!await _repository.ForceCloseAsync(id))
        {
            throw AppException.Conflict("โต๊ะนี้ปิดแล้ว");
        }

        return await GetAsync(id);
    }

    // ---------- ร้าน: โต๊ะ ----------

    public Task<IReadOnlyList<DiningTable>> GetTablesAsync()
        => _repository.GetTablesAsync();

    public async Task<DiningTable> GetTableAsync(Guid id)
        => await _repository.GetTableAsync(id) ?? throw AppException.NotFound("ไม่พบโต๊ะ");

    public Task<DiningTable> CreateTableAsync(TableRequest request)
        => _repository.CreateTableAsync(request);

    public async Task<DiningTable> UpdateTableAsync(Guid id, TableRequest request)
        => await _repository.UpdateTableAsync(id, request) ?? throw AppException.NotFound("ไม่พบโต๊ะ");

    public async Task DeleteTableAsync(Guid id)
    {
        if (!await _repository.DeleteTableAsync(id))
        {
            throw AppException.NotFound("ไม่พบโต๊ะ");
        }
    }
}
