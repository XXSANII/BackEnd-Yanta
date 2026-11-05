using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Products;
using YantaBackEnd.Repositories;

namespace YantaBackEnd.Services;

public sealed class OptionGroupService
{
    private readonly OptionGroupRepository _repository;

    public OptionGroupService(OptionGroupRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<OptionGroup>> GetAllAsync()
        => _repository.GetAllAsync();

    public async Task<OptionGroup> GetAsync(Guid id)
        => await _repository.GetAsync(id)
           ?? throw AppException.NotFound("ไม่พบชุดตัวเลือก");

    public async Task<OptionGroup> CreateAsync(OptionGroupRequest request)
    {
        Validate(request);
        var id = await _repository.CreateAsync(request);
        return await GetAsync(id);
    }

    public async Task<OptionGroup> UpdateAsync(Guid id, OptionGroupRequest request)
    {
        Validate(request);

        if (!await _repository.UpdateAsync(id, request))
        {
            throw AppException.NotFound("ไม่พบชุดตัวเลือก");
        }

        return await GetAsync(id);
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!await _repository.DeleteAsync(id))
        {
            throw AppException.NotFound("ไม่พบชุดตัวเลือก");
        }
    }

    private static void Validate(OptionGroupRequest request)
    {
        request.Name = request.Name.Trim();
        if (request.Name.Length == 0)
        {
            throw AppException.BadRequest("กรุณากรอกชื่อชุดตัวเลือก");
        }

        if (!OptionGroupKind.All.Contains(request.Kind))
        {
            throw AppException.BadRequest("ชนิดชุดตัวเลือกไม่ถูกต้อง");
        }

        if (request.IsRequired && request.Kind != OptionGroupKind.Single)
        {
            throw AppException.BadRequest("ตั้งเป็นบังคับเลือกได้เฉพาะชุดแบบเลือกได้อย่างเดียว");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();

        foreach (var option in request.Options)
        {
            option.Name = option.Name.Trim();
            if (option.Name.Length == 0)
            {
                throw AppException.BadRequest("กรุณากรอกชื่อตัวเลือก");
            }

            if (!names.Add(option.Name))
            {
                throw AppException.BadRequest($"ชื่อตัวเลือก \"{option.Name}\" ซ้ำในชุดเดียวกัน");
            }

            if (option.Id.HasValue && !ids.Add(option.Id.Value))
            {
                throw AppException.BadRequest("พบตัวเลือกซ้ำในคำขอ");
            }

            if (request.Kind == OptionGroupKind.Remove && option.PriceDelta != 0)
            {
                throw AppException.BadRequest("ชุดแบบ \"ไม่ใส่\" ต้องไม่มีราคาเพิ่ม");
            }
        }
    }
}
