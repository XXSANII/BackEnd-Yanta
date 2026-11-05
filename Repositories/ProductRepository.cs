using Dapper;
using YantaBackEnd.Data;
using YantaBackEnd.Models.Products;

namespace YantaBackEnd.Repositories;

public sealed class ProductRepository
{
    private const string CategoryColumns = "id, name, description, sort_order, created_at";

    private const string ProductColumns =
        "id, category_id, name, description, price, image_url, is_available, created_at, updated_at";

    private readonly DapperContext _db;
    private readonly OptionGroupRepository _optionGroups;

    public ProductRepository(DapperContext db, OptionGroupRepository optionGroups)
    {
        _db = db;
        _optionGroups = optionGroups;
    }

    private async Task<IReadOnlyList<Product>> AttachOptionGroupsAsync(IReadOnlyList<Product> products)
    {
        if (products.Count == 0)
        {
            return products;
        }

        var groups = await _optionGroups.GetByProductIdsAsync(products.Select(p => p.Id).ToArray());
        foreach (var product in products)
        {
            product.OptionGroups = groups[product.Id].ToList();
        }

        return products;
    }

    // ---------- Categories ----------

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync()
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<Category>(
            $"SELECT {CategoryColumns} FROM public.categories ORDER BY sort_order, name");
        return rows.AsList();
    }

    public async Task<Category?> GetCategoryAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Category>(
            $"SELECT {CategoryColumns} FROM public.categories WHERE id = @id", new { id });
    }

    public async Task<Category> CreateCategoryAsync(CategoryRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleAsync<Category>(
            $"""
            INSERT INTO public.categories (name, description, sort_order)
            VALUES (@Name, @Description, @SortOrder)
            RETURNING {CategoryColumns}
            """, request);
    }

    public async Task<Category?> UpdateCategoryAsync(Guid id, CategoryRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Category>(
            $"""
            UPDATE public.categories
            SET name = @Name, description = @Description, sort_order = @SortOrder
            WHERE id = @id
            RETURNING {CategoryColumns}
            """, new { id, request.Name, request.Description, request.SortOrder });
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            "DELETE FROM public.categories WHERE id = @id", new { id }) > 0;
    }

    // ---------- Products ----------

    public async Task<IReadOnlyList<Product>> GetProductsAsync(Guid? categoryId, bool includeUnavailable)
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<Product>(
            $"""
            SELECT {ProductColumns}
            FROM public.products
            WHERE (@categoryId IS NULL OR category_id = @categoryId)
              AND (@includeUnavailable OR is_available)
            ORDER BY name
            """, new { categoryId, includeUnavailable });
        return await AttachOptionGroupsAsync(rows.AsList());
    }

    public async Task<Product?> GetProductAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        var product = await connection.QuerySingleOrDefaultAsync<Product>(
            $"SELECT {ProductColumns} FROM public.products WHERE id = @id", new { id });
        if (product is not null)
        {
            await AttachOptionGroupsAsync([product]);
        }

        return product;
    }

    public async Task<IReadOnlyList<Product>> GetProductsByIdsAsync(Guid[] ids)
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<Product>(
            $"SELECT {ProductColumns} FROM public.products WHERE id = ANY(@ids)", new { ids });
        return await AttachOptionGroupsAsync(rows.AsList());
    }

    public async Task<Product> CreateProductAsync(ProductRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleAsync<Product>(
            $"""
            INSERT INTO public.products (category_id, name, description, price, image_url, is_available)
            VALUES (@CategoryId, @Name, @Description, @Price, @ImageUrl, @IsAvailable)
            RETURNING {ProductColumns}
            """, request);
    }

    public async Task<Product?> UpdateProductAsync(Guid id, ProductRequest request)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"""
            UPDATE public.products
            SET category_id = @CategoryId, name = @Name, description = @Description,
                price = @Price, image_url = @ImageUrl, is_available = @IsAvailable
            WHERE id = @id
            RETURNING {ProductColumns}
            """,
            new
            {
                id,
                request.CategoryId,
                request.Name,
                request.Description,
                request.Price,
                request.ImageUrl,
                request.IsAvailable
            });
    }

    /// <summary>แทนที่การผูกชุดตัวเลือกของเมนูทั้งชุด (ลำดับตามตำแหน่งในอาร์เรย์)</summary>
    public async Task SetOptionGroupsAsync(Guid productId, Guid[] groupIds)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        await connection.ExecuteAsync(
            "DELETE FROM public.product_option_groups WHERE product_id = @productId",
            new { productId }, tx);

        for (var i = 0; i < groupIds.Length; i++)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO public.product_option_groups (product_id, group_id, sort_order)
                VALUES (@productId, @groupId, @sortOrder)
                """, new { productId, groupId = groupIds[i], sortOrder = i }, tx);
        }

        tx.Commit();
    }

    public async Task<Product?> SetAvailabilityAsync(Guid id, bool isAvailable)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"""
            UPDATE public.products SET is_available = @isAvailable
            WHERE id = @id
            RETURNING {ProductColumns}
            """, new { id, isAvailable });
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteAsync(
            "DELETE FROM public.products WHERE id = @id", new { id }) > 0;
    }
}
