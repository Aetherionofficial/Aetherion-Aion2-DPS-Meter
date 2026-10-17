-- Owner-only restore for a revoked license key or an account locked by a device change.
-- The Edge Function checks the owner allowlist before calling this service-role RPC.
create or replace function public.admin_restore_aetherion_license_key_hash(p_key_hash text)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_user_id uuid;
    v_key_rows integer := 0;
    v_changed boolean := false;
begin
    if p_key_hash is null or p_key_hash !~ '^[0-9a-f]{64}$' then
        return false;
    end if;

    select redeemed_by
    into v_user_id
    from public.aetherion_license_keys
    where key_hash = p_key_hash
    for update;
    if not found then
        return false;
    end if;

    update public.aetherion_license_keys
    set revoked_at = null
    where key_hash = p_key_hash and revoked_at is not null;
    get diagnostics v_key_rows = row_count;
    v_changed := v_key_rows > 0;

    if v_user_id is not null then
        update public.aetherion_entitlements
        set is_revoked = false, revoked_at = null
        where user_id = v_user_id and (is_revoked or revoked_at is not null);
        get diagnostics v_key_rows = row_count;
        v_changed := v_changed or v_key_rows > 0;

        -- Let the user's next successful login bind this account to the installation they intend to use.
        delete from public.aetherion_account_installations
        where user_id = v_user_id;
        get diagnostics v_key_rows = row_count;
        v_changed := v_changed or v_key_rows > 0;
    end if;

    return v_changed;
end;
$$;

revoke all on function public.admin_restore_aetherion_license_key_hash(text) from public, anon, authenticated;
grant execute on function public.admin_restore_aetherion_license_key_hash(text) to service_role;
