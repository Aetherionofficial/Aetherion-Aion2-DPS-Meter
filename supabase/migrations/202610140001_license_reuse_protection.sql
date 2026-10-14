-- Private registry used by the aetherion-license-check Edge Function.
create table if not exists public.aetherion_license_bindings (
    license_id text primary key check (license_id ~ '^[0-9a-fA-F]{32}$'),
    installation_id text not null check (installation_id ~ '^[0-9a-fA-F]{24}$'),
    state text not null default 'active' check (state in ('active', 'revoked')),
    first_seen_at timestamptz not null default now(),
    revoked_at timestamptz
);

alter table public.aetherion_license_bindings enable row level security;
revoke all on table public.aetherion_license_bindings from anon, authenticated;

-- The Edge Function verifies the owner signature, then atomically claims or revokes the license.
create or replace function public.check_aetherion_license_binding(
    p_license_id text,
    p_signed_installation_id text,
    p_current_installation_id text
) returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
    binding public.aetherion_license_bindings%rowtype;
begin
    if p_license_id !~ '^[0-9a-fA-F]{32}$'
       or p_signed_installation_id !~ '^[0-9a-fA-F]{24}$'
       or p_current_installation_id !~ '^[0-9a-fA-F]{24}$' then
        return false;
    end if;

    insert into public.aetherion_license_bindings (license_id, installation_id)
    values (lower(p_license_id), upper(p_signed_installation_id))
    on conflict (license_id) do nothing;

    select * into binding
    from public.aetherion_license_bindings
    where license_id = lower(p_license_id)
    for update;

    if binding.state = 'revoked'
       or upper(p_signed_installation_id) <> upper(p_current_installation_id)
       or binding.installation_id <> upper(p_signed_installation_id) then
        update public.aetherion_license_bindings
        set state = 'revoked', revoked_at = coalesce(revoked_at, now())
        where license_id = lower(p_license_id);
        return false;
    end if;

    return true;
end;
$$;

revoke all on function public.check_aetherion_license_binding(text, text, text) from public, anon, authenticated;
grant execute on function public.check_aetherion_license_binding(text, text, text) to service_role;
