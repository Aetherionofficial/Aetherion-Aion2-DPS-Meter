-- Run in Supabase SQL Editor. Safe to re-run after the first installation.
-- Key types accepted by issue_aetherion_license_key:
-- trial_1d, week_1w, days_30, days_90, lifetime, partner

create schema if not exists extensions;
create extension if not exists pgcrypto with schema extensions;

create table if not exists public.aetherion_license_keys (
    key_hash text primary key,
    duration_days integer,
    license_type text not null default 'days_30',
    issued_at timestamptz not null default now(),
    redeemed_at timestamptz,
    redeemed_by uuid references auth.users (id) on delete set null,
    revoked_at timestamptz
);

create table if not exists public.aetherion_entitlements (
    user_id uuid primary key references auth.users (id) on delete cascade,
    expires_at timestamptz,
    last_redeemed_at timestamptz not null default now(),
    license_type text not null default 'days_30',
    is_partner boolean not null default false,
    is_revoked boolean not null default false,
    revoked_at timestamptz
);

-- Upgrade the earlier 0.3.14 trial schema without losing existing accounts or keys.
alter table public.aetherion_license_keys alter column duration_days drop not null;
alter table public.aetherion_license_keys add column if not exists license_type text not null default 'days_30';
alter table public.aetherion_license_keys add column if not exists revoked_at timestamptz;
alter table public.aetherion_entitlements alter column expires_at drop not null;
alter table public.aetherion_entitlements add column if not exists license_type text not null default 'days_30';
alter table public.aetherion_entitlements add column if not exists is_partner boolean not null default false;
alter table public.aetherion_entitlements add column if not exists is_revoked boolean not null default false;
alter table public.aetherion_entitlements add column if not exists revoked_at timestamptz;

alter table public.aetherion_license_keys enable row level security;
alter table public.aetherion_entitlements enable row level security;
revoke all on table public.aetherion_license_keys from public, anon, authenticated;
revoke all on table public.aetherion_entitlements from public, anon, authenticated;
grant select on table public.aetherion_entitlements to authenticated;

drop policy if exists "Users can read their own Aetherion entitlement" on public.aetherion_entitlements;
create policy "Users can read their own Aetherion entitlement"
    on public.aetherion_entitlements for select to authenticated
    using ((select auth.uid()) = user_id);

drop function if exists public.issue_aetherion_license_key(integer);
create or replace function public.issue_aetherion_license_key(p_license_type text)
returns text
language plpgsql security definer set search_path = ''
as $$
declare
    v_key text;
    v_days integer;
begin
    v_days := case p_license_type
        when 'trial_1d' then 1
        when 'week_1w' then 7
        when 'days_30' then 30
        when 'days_90' then 90
        when 'lifetime' then null
        when 'partner' then null
        else null
    end;
    if p_license_type not in ('trial_1d', 'week_1w', 'days_30', 'days_90', 'lifetime', 'partner') then
        raise exception 'LICENSE_TYPE_INVALID';
    end if;

    v_key := 'AETH-' || pg_catalog.upper(pg_catalog.encode(extensions.gen_random_bytes(18), 'hex'));
    insert into public.aetherion_license_keys (key_hash, duration_days, license_type)
    values (
        pg_catalog.encode(extensions.digest(pg_catalog.convert_to(v_key, 'UTF8'), 'sha256'), 'hex'),
        v_days, p_license_type
    );
    return v_key;
end;
$$;
revoke all on function public.issue_aetherion_license_key(text) from public, anon, authenticated, service_role;
grant execute on function public.issue_aetherion_license_key(text) to postgres;

create or replace function public.redeem_aetherion_license_key(p_license_key text)
returns timestamptz
language plpgsql security definer set search_path = ''
as $$
declare
    v_user_id uuid := auth.uid();
    v_hash text;
    v_type text;
    v_days integer;
    v_expiry timestamptz;
    v_partner boolean;
begin
    if v_user_id is null then raise exception 'AUTH_REQUIRED'; end if;
    if p_license_key is null or pg_catalog.length(pg_catalog.btrim(p_license_key)) < 10 then
        raise exception 'KEY_INVALID_OR_REDEEMED';
    end if;

    v_hash := pg_catalog.encode(extensions.digest(
        pg_catalog.convert_to(pg_catalog.upper(pg_catalog.btrim(p_license_key)), 'UTF8'), 'sha256'), 'hex');
    select k.license_type, k.duration_days into v_type, v_days
    from public.aetherion_license_keys k
    where k.key_hash = v_hash and k.redeemed_at is null and k.revoked_at is null
    for update;
    if not found then raise exception 'KEY_INVALID_OR_REDEEMED'; end if;

    -- Permanent access (including Partner) cannot redeem another code.
    if exists (
        select 1 from public.aetherion_entitlements e
        where e.user_id = v_user_id and not e.is_revoked
          and (e.expires_at is null or e.is_partner)
    ) then
        raise exception 'PERMANENT_LICENSE_ALREADY_ACTIVE';
    end if;

    v_partner := v_type = 'partner';
    insert into public.aetherion_entitlements
        (user_id, expires_at, last_redeemed_at, license_type, is_partner, is_revoked, revoked_at)
    values (
        v_user_id,
        case when v_days is null then null else pg_catalog.now() + pg_catalog.make_interval(days => v_days) end,
        pg_catalog.now(), v_type, v_partner, false, null
    )
    on conflict (user_id) do update
    set expires_at = case
            when excluded.expires_at is null then null
            else greatest(coalesce(public.aetherion_entitlements.expires_at, pg_catalog.now()), pg_catalog.now())
                 + pg_catalog.make_interval(days => v_days)
        end,
        last_redeemed_at = pg_catalog.now(),
        license_type = excluded.license_type,
        is_partner = excluded.is_partner,
        is_revoked = false,
        revoked_at = null
    returning expires_at into v_expiry;

    update public.aetherion_license_keys
    set redeemed_at = pg_catalog.now(), redeemed_by = v_user_id
    where key_hash = v_hash and redeemed_at is null and revoked_at is null;
    if not found then raise exception 'KEY_INVALID_OR_REDEEMED'; end if;
    return v_expiry;
end;
$$;
revoke all on function public.redeem_aetherion_license_key(text) from public, anon;
grant execute on function public.redeem_aetherion_license_key(text) to authenticated;

-- Owner-only: revoke one issued key. If it was redeemed, the linked account loses access too.
create or replace function public.revoke_aetherion_license_key(p_license_key text)
returns boolean
language plpgsql security definer set search_path = ''
as $$
declare
    v_hash text;
    v_user_id uuid;
begin
    if p_license_key is null or pg_catalog.length(pg_catalog.btrim(p_license_key)) < 10 then return false; end if;
    v_hash := pg_catalog.encode(extensions.digest(
        pg_catalog.convert_to(pg_catalog.upper(pg_catalog.btrim(p_license_key)), 'UTF8'), 'sha256'), 'hex');
    update public.aetherion_license_keys
    set revoked_at = pg_catalog.now()
    where key_hash = v_hash and revoked_at is null
    returning redeemed_by into v_user_id;
    if not found then return false; end if;
    if v_user_id is not null then
        update public.aetherion_entitlements
        set is_revoked = true, revoked_at = pg_catalog.now()
        where user_id = v_user_id;
    end if;
    return true;
end;
$$;
revoke all on function public.revoke_aetherion_license_key(text) from public, anon, authenticated, service_role;
grant execute on function public.revoke_aetherion_license_key(text) to postgres;

-- Previous manual approval rows remain as history and do not grant access.
