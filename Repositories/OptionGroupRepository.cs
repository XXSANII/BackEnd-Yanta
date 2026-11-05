using Dapper;
using YantaBackEnd.Data;
using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Products;

namespace YantaBackEnd.Repositories;

public sealed class OptionGroupRepository
{
    private readonly DapperContext _db;

    public OptionGroupRepository(DapperContext db)
    {
        _db = db;
    }

    private sealed class GroupRow
    {
        public Guid ProductId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>ชุดตัวเลือกของแต่ละเมนู (key = productId) รวมตัวเลือกที่ปิดขาย</summary>
    public async Task<ILookup<Guid, OptionGroup>> GetByProductIdsAsync(Guid[] productIds)
    {
        if (productIds.Length == 0)
        {
            return Array.Empty<OptionGroup>().ToLookup(g => g.Id);
        }

        using var connection = _db.CreateConnection();

        var groupRows = (await connection.QueryAsync<GroupRow>(
            """
            SELECT pog.product_id, g.id, g.name, g.kind, g.is_required, pog.sort_order
            FROM public.product_option_groups pog
            JOIN public.option_groups g ON g.id = pog.group_id
            WHERE pog.product_id = ANY(@productIds)
            ORDER BY pog.sort_order, g.name
            """, new { productIds })).AsList();

        if (groupRows.Count == 0)
        {
            return Array.Empty<OptionGroup>().ToLookup(g => g.Id);
        }

        var groupIds = groupRows.Select(r => r.Id).Distinct().ToArray();

        var options = (await connection.QueryAsync<ProductOption>(
            """
            SELECT id, group_id, name, price_delta, is_available, sort_order
            FROM public.options
            WHERE group_id = ANY(@groupIds)
            ORDER BY sort_order, name
            """, new { groupIds })).AsList();

        var optionsByGroup = options.ToLookup(o => o.GroupId);

        // ชุดเดียวกันอาจผูกกับหลายเมนู: สร้างอ็อบเจ็กต์แยกต่อเมนู (ลำดับแถวคงตามที่ query)
        return groupRows.ToLookup(
            r => r.ProductId,
            r => new OptionGroup
            {
                Id = r.Id,
                Name = r.Name,
                Kind = r.Kind,
                IsRequired = r.IsRequired,
                SortOrder = r.SortOrder,
                Options = optionsByGroup[r.Id].ToList()
            });
    }

    // ---------- จัดการชุดตัวเลือก (ร้าน) ----------

    private const string OptionColumns = "id, group_id, name, price_delta, is_available, sort_order";

    private sealed class GroupHeader
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }

    private static OptionGroup ToGroup(GroupHeader h, IEnumerable<ProductOption> options) => new()
    {
        Id = h.Id,
        Name = h.Name,
        Kind = h.Kind,
        IsRequired = h.IsRequired,
        SortOrder = h.SortOrder,
        Options = options.ToList()
    };

    public async Task<IReadOnlyList<OptionGroup>> GetAllAsync()
    {
        using var connection = _db.CreateConnection();

        var headers = (await connection.QueryAsync<GroupHeader>(
            """
            SELECT id, name, kind, is_required, sort_order
            FROM public.option_groups
            ORDER BY sort_order, name
            """)).AsList();

        var options = await connection.QueryAsync<ProductOption>(
            $"SELECT {OptionColumns} FROM public.options ORDER BY sort_order, name");
        var byGroup = options.ToLookup(o => o.GroupId);

        return headers.Select(h => ToGroup(h, byGroup[h.Id])).ToList();
    }

    public async Task<OptionGroup?> GetAsync(Guid id)
    {
        using var connection = _db.CreateConnection();

        var header = await connection.QuerySingleOrDefaultAsync<GroupHeader>(
            """
            SELECT id, name, kind, is_required, sort_order
            FROM public.option_groups WHERE id = @id
            """, new { id });
        if (header is null)
        {
            return null;
        }

        var options = await connection.QueryAsync<ProductOption>(
            $"SELECT {OptionColumns} FROM public.options WHERE group_id = @id ORDER BY sort_order, name",
            new { id });
        return ToGroup(header, options);
    }

    /// <summary>จำนวน id ที่มีอยู่จริงในตาราง option_groups</summary>
    public async Task<int> CountExistingAsync(Guid[] ids)
    {
        if (ids.Length == 0)
        {
            return 0;
        }

        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.option_groups WHERE id = ANY(@ids)", new { ids });
    }

    public async Task<Guid> CreateAsync(OptionGroupRequest request)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var id = await connection.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO public.option_groups (name, kind, is_required, sort_order)
            VALUES (@Name, @Kind, @IsRequired, @SortOrder)
            RETURNING id
            """, request, tx);

        foreach (var option in request.Options)
        {
            await InsertOptionAsync(connection, tx, id, option);
        }

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, OptionGroupRequest request)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var updated = await connection.ExecuteAsync(
            """
            UPDATE public.option_groups
            SET name = @Name, kind = @Kind, is_required = @IsRequired,
                sort_order = @SortOrder, updated_at = now()
            WHERE id = @id
            """, new { id, request.Name, request.Kind, request.IsRequired, request.SortOrder }, tx);
        if (updated == 0)
        {
            return false;
        }

        var existingIds = (await connection.QueryAsync<Guid>(
            "SELECT id FROM public.options WHERE group_id = @id", new { id }, tx)).ToHashSet();

        // id ที่ส่งมาต้องเป็นตัวเลือกของชุดนี้เท่านั้น
        foreach (var option in request.Options.Where(o => o.Id.HasValue))
        {
            if (!existingIds.Contains(option.Id!.Value))
            {
                throw AppException.BadRequest("ตัวเลือกไม่ได้อยู่ในชุดนี้");
            }
        }

        var keepIds = request.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToArray();

        // ตัวเลือกที่ขาดจากคำขอ = ลบ
        await connection.ExecuteAsync(
            "DELETE FROM public.options WHERE group_id = @id AND NOT (id = ANY(@keepIds))",
            new { id, keepIds }, tx);

        foreach (var option in request.Options)
        {
            if (option.Id.HasValue)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE public.options
                    SET name = @Name, price_delta = @PriceDelta,
                        is_available = @IsAvailable, sort_order = @SortOrder
                    WHERE id = @optionId AND group_id = @groupId
                    """,
                    new
                    {
                        optionId = option.Id.Value,
                        groupId = id,
                        option.Name,
                        option.PriceDelta,
                        option.IsAvailable,
                        option.SortOrder
                    }, tx);
            }
            else
            {
                await InsertOptionAsync(connection, tx, id, option);
            }
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            "DELETE FROM public.option_groups WHERE id = @id", new { id }) > 0;
    }

    private static Task<int> InsertOptionAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction tx, Guid groupId, OptionRequest option)
        => connection.ExecuteAsync(
            """
            INSERT INTO public.options (group_id, name, price_delta, is_available, sort_order)
            VALUES (@groupId, @Name, @PriceDelta, @IsAvailable, @SortOrder)
            """,
            new { groupId, option.Name, option.PriceDelta, option.IsAvailable, option.SortOrder }, tx);
}
