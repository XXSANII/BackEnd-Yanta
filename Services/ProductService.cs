using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Products;
using YantaBackEnd.Repositories;

namespace YantaBackEnd.Services;

public sealed class ProductService
{
    private readonly ProductRepository _repository;
    private readonly OptionGroupRepository _optionGroups;

    public ProductService(ProductRepository repository, OptionGroupRepository optionGroups)
    {
        _repository = repository;
        _optionGroups = optionGroups;
    }

    // ---------- Categories ----------

    public Task<IReadOnlyList<Category>> GetCategoriesAsync()
        => _repository.GetCategoriesAsync();

    public async Task<Category> GetCategoryAsync(Guid id)
        => await _repository.GetCategoryAsync(id)
           ?? throw AppException.NotFound("ไม่พบหมวดหมู่");

    public Task<Category> CreateCategoryAsync(CategoryRequest request)
        => _repository.CreateCategoryAsync(request);

    public async Task<Category> UpdateCategoryAsync(Guid id, CategoryRequest request)
        => await _repository.UpdateCategoryAsync(id, request)
           ?? throw AppException.NotFound("ไม่พบหมวดหมู่");

    public async Task DeleteCategoryAsync(Guid id)
    {
        if (!await _repository.DeleteCategoryAsync(id))
        {
            throw AppException.NotFound("ไม่พบหมวดหมู่");
        }
    }

    // ---------- Products ----------

    public async Task<IReadOnlyList<Product>> GetProductsAsync(Guid? categoryId, bool includeUnavailable)
    {
        var products = await _repository.GetProductsAsync(categoryId, includeUnavailable);

        if (!includeUnavailable)
        {
            foreach (var product in products)
            {
                HideUnavailableOptions(product);
            }
        }

        return products;
    }

    public async Task<Product> GetProductAsync(Guid id, bool includeUnavailable)
    {
        var product = await _repository.GetProductAsync(id);

        if (product is null || (!product.IsAvailable && !includeUnavailable))
        {
            throw AppException.NotFound("ไม่พบเมนู");
        }

        if (!includeUnavailable)
        {
            HideUnavailableOptions(product);
        }

        return product;
    }

    /// <summary>ตัดตัวเลือกที่ปิดขาย และชุดที่เหลือ 0 ตัวเลือก ออกจากมุมมองลูกค้า</summary>
    private static void HideUnavailableOptions(Product product)
    {
        foreach (var group in product.OptionGroups)
        {
            group.Options.RemoveAll(o => !o.IsAvailable);
        }

        product.OptionGroups.RemoveAll(g => g.Options.Count == 0);
    }

    public async Task<Product> CreateProductAsync(ProductRequest request)
    {
        await EnsureCategoryExistsAsync(request.CategoryId);
        var groupIds = await ValidateOptionGroupIdsAsync(request.OptionGroupIds);

        var product = await _repository.CreateProductAsync(request);
        if (groupIds is null)
        {
            return product;
        }

        await _repository.SetOptionGroupsAsync(product.Id, groupIds);
        return await _repository.GetProductAsync(product.Id) ?? product;
    }

    public async Task<Product> UpdateProductAsync(Guid id, ProductRequest request)
    {
        await EnsureCategoryExistsAsync(request.CategoryId);
        var groupIds = await ValidateOptionGroupIdsAsync(request.OptionGroupIds);

        var product = await _repository.UpdateProductAsync(id, request)
                      ?? throw AppException.NotFound("ไม่พบเมนู");
        if (groupIds is null)
        {
            return product;
        }

        await _repository.SetOptionGroupsAsync(product.Id, groupIds);
        return await _repository.GetProductAsync(product.Id) ?? product;
    }

    /// <summary>null = ไม่แตะ; ตัด id ซ้ำ และตรวจว่าทุก id มีอยู่จริง</summary>
    private async Task<Guid[]?> ValidateOptionGroupIdsAsync(Guid[]? ids)
    {
        if (ids is null)
        {
            return null;
        }

        var distinct = ids.Distinct().ToArray();
        if (await _optionGroups.CountExistingAsync(distinct) != distinct.Length)
        {
            throw AppException.BadRequest("ไม่พบชุดตัวเลือกที่ระบุ");
        }

        return distinct;
    }

    public async Task<Product> SetAvailabilityAsync(Guid id, bool isAvailable)
        => await _repository.SetAvailabilityAsync(id, isAvailable)
           ?? throw AppException.NotFound("ไม่พบเมนู");

    public async Task DeleteProductAsync(Guid id)
    {
        if (!await _repository.DeleteProductAsync(id))
        {
            throw AppException.NotFound("ไม่พบเมนู");
        }
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId)
    {
        if (await _repository.GetCategoryAsync(categoryId) is null)
        {
            throw AppException.BadRequest("ไม่พบหมวดหมู่ที่ระบุ");
        }
    }
}
