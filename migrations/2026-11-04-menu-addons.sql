-- Menu add-ons (option groups) migration. Additive only; does not touch existing data.
-- Run manually in the Supabase SQL Editor.

create table public.option_groups (
  id uuid primary key default gen_random_uuid(),
  name varchar(100) not null,
  kind text not null check (kind in ('addon','remove','single')),
  is_required boolean not null default false,   -- มีผลเฉพาะ kind = 'single'
  sort_order integer not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table public.options (
  id uuid primary key default gen_random_uuid(),
  group_id uuid not null references public.option_groups(id) on delete cascade,
  name varchar(100) not null,
  price_delta numeric(10,2) not null default 0 check (price_delta >= 0),
  is_available boolean not null default true,
  sort_order integer not null default 0
);

create table public.product_option_groups (
  product_id uuid not null references public.products(id) on delete cascade,
  group_id uuid not null references public.option_groups(id) on delete cascade,
  sort_order integer not null default 0,
  primary key (product_id, group_id)
);

alter table public.order_items
  add column options jsonb not null default '[]'::jsonb,  -- [{groupName, name, priceDelta}] สำเนา ณ ตอนสั่ง
  add column note varchar(200);

create index on public.options(group_id);
create index on public.product_option_groups(group_id);

-- สอดคล้องกับตารางอื่น: เปิด RLS ไม่มี policy (Backend ต่อด้วยสิทธิ์ postgres; anon เข้าตรงไม่ได้)
alter table public.option_groups enable row level security;
alter table public.options enable row level security;
alter table public.product_option_groups enable row level security;
