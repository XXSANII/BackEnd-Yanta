-- Sample data for testing menu add-ons. Optional; run separately AFTER 2026-11-04-menu-addons.sql.
-- Links "ท็อปปิ้ง" + "ไม่ใส่" to every product in category 'ข้าวต้ม',
-- and "ขนาด" (required single) to the product 'ข้าวต้มกุ้ง' only.
-- Safe to re-run: groups/options are inserted only if not already present (matched by name),
-- links use on conflict do nothing. If the category/product is missing, nothing is linked.

begin;

-- Option groups (skip if a group with the same name+kind already exists)
insert into public.option_groups (name, kind, is_required, sort_order)
select v.name, v.kind, v.is_required, v.sort_order
from (values
  ('ท็อปปิ้ง', 'addon',  false, 1),
  ('ไม่ใส่',    'remove', false, 2),
  ('ขนาด',     'single', true,  3)
) as v(name, kind, is_required, sort_order)
where not exists (
  select 1 from public.option_groups g where g.name = v.name and g.kind = v.kind
);

-- Options (skip if the group already has an option with the same name)
insert into public.options (group_id, name, price_delta, sort_order)
select g.id, v.name, v.price_delta, v.sort_order
from (values
  ('ท็อปปิ้ง', 'addon',  'เพิ่มไข่',   10, 1),
  ('ท็อปปิ้ง', 'addon',  'เพิ่มปลา',   20, 2),
  ('ท็อปปิ้ง', 'addon',  'เพิ่มหมู',   20, 3),
  ('ไม่ใส่',    'remove', 'ไม่ใส่ผัก',     0, 1),
  ('ไม่ใส่',    'remove', 'ไม่ใส่ต้นหอม',  0, 2),
  ('ขนาด',     'single', 'ธรรมดา',     0, 1),
  ('ขนาด',     'single', 'พิเศษ',      15, 2)
) as v(group_name, kind, name, price_delta, sort_order)
join public.option_groups g on g.name = v.group_name and g.kind = v.kind
where not exists (
  select 1 from public.options o where o.group_id = g.id and o.name = v.name
);

-- Link "ท็อปปิ้ง" + "ไม่ใส่" to all products in category 'ข้าวต้ม' (no rows if category is missing)
insert into public.product_option_groups (product_id, group_id, sort_order)
select p.id, g.id, g.sort_order
from public.products p
join public.categories c on c.id = p.category_id
join public.option_groups g on (g.name, g.kind) in (('ท็อปปิ้ง','addon'), ('ไม่ใส่','remove'))
where c.name = 'ข้าวต้ม'
on conflict (product_id, group_id) do nothing;

-- Link "ขนาด" (required single) to the single product 'ข้าวต้มกุ้ง' in that category (no rows if missing)
insert into public.product_option_groups (product_id, group_id, sort_order)
select p.id, g.id, g.sort_order
from public.products p
join public.categories c on c.id = p.category_id
join public.option_groups g on g.name = 'ขนาด' and g.kind = 'single'
where c.name = 'ข้าวต้ม' and p.name = 'ข้าวต้มกุ้ง'
on conflict (product_id, group_id) do nothing;

commit;
