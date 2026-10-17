-- Owner-only primitives for the Aetherion license dashboard.
-- The Edge Function verifies the signed-in owner allowlist before calling these RPCs.

create or replace function public.admin_issue_aetherion_license_keys(
    p_license_type text,
    p_count integer default 1
)
returns table (
    license_key text,
    key_hash text,
    license_type text,
    duration_days integer,
    issued_at timestamptz
)
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_key text;
    v_hash text;
    v_days integer;
    v_now timestamptz;
    v_i integer;
begin
    if p_count is null or p_count < 1 or p_count > 50 then
        raise exception 'LICENSE_BATCH_SIZE_INVALID';
    end if;

    v_days := case p_license_type
        when 'trial_1d' then 1
        when 'week_1w' then 7
        when 'days_30' then 30
        when 'days_90' then 90
        when 'lifetime' then null
        when 'partner' then null
        else null
    end;
    if p_license_type is null or p_license_type not in ('trial_1d', 'week_1w', 'days_30', 'days_90', 'lifetime', 'partner') then
        raise exception 'LICENSE_TYPE_INVALID';
    end if;

    for v_i in 1..p_count loop
        v_key := 'AETH-' || pg_catalog.upper(pg_catalog.encode(extensions.gen_random_bytes(18), 'hex'));
        v_hash := pg_catalog.encode(extensions.digest(pg_catalog.convert_to(v_key, 'UTF8'), 'sha256'), 'hex');
        v_now := pg_catalog.now();
        insert into public.aetherion_license_keys (key_hash, duration_days, license_type, issued_at)
        values (v_hash, v_days, p_license_type, v_now);
        license_key := v_key;
        key_hash := v_hash;
        license_type := p_license_type;
        duration_days := v_days;
        issued_at := v_now;
        return next;
    end loop;
end;
$$;

revoke all on function public.admin_issue_aetherion_license_keys(text, integer) from public, anon, authenticated;
grant execute on function public.admin_issue_aetherion_license_keys(text, integer) to service_role;

create or replace function public.admin_revoke_aetherion_license_key_hash(p_key_hash text)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_user_id uuid;
begin
    if p_key_hash is null or p_key_hash !~ '^[0-9a-f]{64}$' then
        return false;
    end if;

    update public.aetherion_license_keys
    set revoked_at = pg_catalog.now()
    where key_hash = p_key_hash and revoked_at is null
    returning redeemed_by into v_user_id;
    if not found then
        return false;
    end if;

    if v_user_id is not null then
        update public.aetherion_entitlements
        set is_revoked = true, revoked_at = pg_catalog.now()
        where user_id = v_user_id;
    end if;
    return true;
end;
$$;

revoke all on function public.admin_revoke_aetherion_license_key_hash(text) from public, anon, authenticated;
grant execute on function public.admin_revoke_aetherion_license_key_hash(text) to service_role;
