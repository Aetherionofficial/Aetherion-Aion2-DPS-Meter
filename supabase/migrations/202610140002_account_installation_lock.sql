-- Bind each active Discord entitlement to its first Aetherion installation.
-- A different installation automatically revokes the account entitlement.
create table if not exists public.aetherion_account_installations (
    user_id uuid primary key references auth.users (id) on delete cascade,
    installation_id text not null check (installation_id ~ '^[0-9A-Fa-f]{24}$'),
    state text not null default 'active' check (state in ('active', 'revoked')),
    first_seen_at timestamptz not null default now(),
    revoked_at timestamptz
);

alter table public.aetherion_account_installations enable row level security;
revoke all on table public.aetherion_account_installations from public, anon, authenticated;

create or replace function public.check_aetherion_account_installation(
    p_user_id uuid,
    p_installation_id text
) returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
    binding public.aetherion_account_installations%rowtype;
begin
    if p_user_id is null or p_installation_id !~ '^[0-9A-Fa-f]{24}$' then
        return false;
    end if;

    if not exists (
        select 1 from public.aetherion_entitlements e
        where e.user_id = p_user_id
          and not e.is_revoked
          and (e.expires_at is null or e.expires_at > now())
    ) then
        return false;
    end if;

    insert into public.aetherion_account_installations (user_id, installation_id)
    values (p_user_id, upper(p_installation_id))
    on conflict (user_id) do nothing;

    select * into binding
    from public.aetherion_account_installations
    where user_id = p_user_id
    for update;

    if binding.state = 'revoked' or binding.installation_id <> upper(p_installation_id) then
        update public.aetherion_account_installations
        set state = 'revoked', revoked_at = coalesce(revoked_at, now())
        where user_id = p_user_id;
        update public.aetherion_entitlements
        set is_revoked = true, revoked_at = coalesce(revoked_at, now())
        where user_id = p_user_id;
        return false;
    end if;

    return true;
end;
$$;

revoke all on function public.check_aetherion_account_installation(uuid, text) from public, anon, authenticated;
grant execute on function public.check_aetherion_account_installation(uuid, text) to service_role;
