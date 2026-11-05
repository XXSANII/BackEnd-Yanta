using System.Text.Json;
using YantaBackEnd.Middleware;
using YantaBackEnd.Models.Orders;
using YantaBackEnd.Models.Products;

namespace YantaBackEnd.Services;

/// <summary>ตรวจบรรทัดสั่งอาหารและคิดราคา ณ ตอนสั่ง (ไม่แตะฐานข้อมูล)</summary>
public static class OrderLineBuilder
{
    private const int MaxNoteLength = 200;
    private const int MaxQuantity = 99;

    public static NewOrderLine Build(Product product, OrderLineRequest line)
    {
        if (!product.IsAvailable)
        {
            throw AppException.BadRequest($"เมนู \"{product.Name}\" ไม่พร้อมจำหน่าย");
        }

        var note = line.Note?.Trim();
        note = string.IsNullOrEmpty(note) ? null : note;

        if (note is { Length: > MaxNoteLength })
        {
            throw AppException.BadRequest($"หมายเหตุของเมนู \"{product.Name}\" ยาวเกิน {MaxNoteLength} ตัวอักษร");
        }

        var optionIds = line.OptionIds ?? [];

        if (optionIds.Distinct().Count() != optionIds.Length)
        {
            throw AppException.BadRequest($"เลือกตัวเลือกซ้ำกันในเมนู \"{product.Name}\"");
        }

        var selected = new List<(OptionGroup Group, ProductOption Option)>();

        foreach (var optionId in optionIds)
        {
            OptionGroup? group = null;
            ProductOption? option = null;

            foreach (var g in product.OptionGroups)
            {
                option = g.Options.FirstOrDefault(o => o.Id == optionId);

                if (option is not null)
                {
                    group = g;
                    break;
                }
            }

            if (group is null || option is null)
            {
                throw AppException.BadRequest($"ตัวเลือกที่เลือกของเมนู \"{product.Name}\" ใช้ไม่ได้แล้ว กรุณานำรายการนี้ออกจากตะกร้าแล้วเลือกใหม่");
            }

            if (!option.IsAvailable)
            {
                throw AppException.Conflict($"ตัวเลือก \"{option.Name}\" ของเมนู \"{product.Name}\" ไม่พร้อมจำหน่ายแล้ว");
            }

            selected.Add((group, option));
        }

        foreach (var group in product.OptionGroups.Where(g => g.Kind == OptionGroupKind.Single))
        {
            var count = selected.Count(s => s.Group.Id == group.Id);

            if (count > 1)
            {
                throw AppException.BadRequest($"ชุด \"{group.Name}\" ของเมนู \"{product.Name}\" เลือกได้ไม่เกิน 1 อย่าง");
            }

            if (count == 0 && group.IsRequired && group.Options.Any(o => o.IsAvailable))
            {
                throw AppException.BadRequest($"กรุณาเลือก \"{group.Name}\" ของเมนู \"{product.Name}\"");
            }
        }

        var unitPrice = product.Price + selected.Sum(s => s.Option.PriceDelta);

        var optionsJson = JsonSerializer.Serialize(
            selected.Select(s => new OrderItemOption(s.Group.Name, s.Option.Name, s.Option.PriceDelta)),
            OrderItemOption.JsonOptions);

        return new NewOrderLine(
            product.Id, product.Name, unitPrice, Math.Clamp(line.Quantity, 1, MaxQuantity), optionsJson, note);
    }
}
