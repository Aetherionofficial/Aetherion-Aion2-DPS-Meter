-- Private audit log and targeted device-binding reset for the owner dashboard.
create table if not exists public.aetherion_license_admin_events (
    id bigint generated always as identity primary key,
    created_at timestamptz not null default pg_catalog.now(),
    actor_user_id uuid not null,
    action text not null check (action in ('key_created', 'key_revoked', 'key_restored', 'device_reset')),
    key_hash text not null check (key_hash ~ '^[0-9a-f]{64}$'),
    account_user_id uuid,
    details text
);

create index if not exists aetherion_license_admin_events_created_at_idx
    on public.aetherion_license_admin_events (created_at desc);

alter table public.aetherion_license_admin_events enable row level security;
revoke all on table public.aetherion_license_admin_events from public, anon, authenticated;
grant select, insert on table public.aetherion_license_admin_events to service_role;
grant usage, select on sequence public.aetherion_license_admin_events_id_seq to service_role;

create or replace function public.admin_reset_aetherion_account_device(p_key_hash text)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_user_id uuid;
    v_deleted integer := 0;
begin
    if p_key_hash is null or p_key_hash !~ '^[0-9a-f]{64}$' then
        return false;
    end if;

    select redeemed_by
    into v_user_id
    from public.aetherion_license_keys
    where key_hash = p_key_hash
    for update;
    if not found or v_user_id is null then
        return false;
    end if;

    delete from public.aetherion_account_installations
    where user_id = v_user_id;
    get diagnostics v_deleted = row_count;
    return v_deleted > 0;
end;
$$;

revoke all on function public.admin_reset_aetherion_account_device(text) from public, anon, authenticated;
grant execute on function public.admin_reset_aetherion_account_device(text) to service_role;
