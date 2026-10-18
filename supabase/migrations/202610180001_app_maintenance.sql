-- App availability is independent of customer entitlements and device bindings.
create table public.aetherion_app_maintenance (
    id boolean primary key default true check (id),
    enabled boolean not null default false,
    message text not null default 'Wir führen gerade Wartungsarbeiten durch. Bitte versuche es später erneut.'
        check (char_length(message) between 1 and 500),
    revision bigint not null default 0 check (revision >= 0),
    updated_at timestamptz not null default pg_catalog.now(),
    updated_by uuid
);
insert into public.aetherion_app_maintenance (id) values (true);
alter table public.aetherion_app_maintenance enable row level security;
revoke all on public.aetherion_app_maintenance from public, anon, authenticated;
grant select on public.aetherion_app_maintenance to service_role;

create table public.aetherion_maintenance_events (
    id bigint generated always as identity primary key,
    created_at timestamptz not null default pg_catalog.now(),
    actor_user_id uuid not null,
    enabled boolean not null,
    message text not null,
    revision bigint not null
);
alter table public.aetherion_maintenance_events enable row level security;
revoke all on public.aetherion_maintenance_events from public, anon, authenticated;
grant select on public.aetherion_maintenance_events to service_role;

create function public.admin_set_aetherion_maintenance(
    p_enabled boolean, p_message text, p_expected_revision bigint, p_actor_user_id uuid
) returns jsonb
language plpgsql security definer set search_path = ''
as $$
declare v_state public.aetherion_app_maintenance%rowtype;
begin
    if p_enabled is null or p_actor_user_id is null or p_expected_revision is null
        or p_message is null or char_length(pg_catalog.btrim(p_message)) not between 1 and 500 then
        raise exception 'MAINTENANCE_INVALID';
    end if;
    select * into strict v_state from public.aetherion_app_maintenance where id = true for update;
    if v_state.revision <> p_expected_revision then raise exception 'MAINTENANCE_CONFLICT'; end if;
    update public.aetherion_app_maintenance
    set enabled = p_enabled, message = pg_catalog.btrim(p_message), revision = revision + 1,
        updated_at = pg_catalog.now(), updated_by = p_actor_user_id
    where id = true returning * into v_state;
    insert into public.aetherion_maintenance_events (actor_user_id, enabled, message, revision)
    values (p_actor_user_id, v_state.enabled, v_state.message, v_state.revision);
    return pg_catalog.jsonb_build_object('enabled', v_state.enabled, 'message', v_state.message,
        'revision', v_state.revision, 'updatedAt', v_state.updated_at);
end;
$$;
revoke all on function public.admin_set_aetherion_maintenance(boolean, text, bigint, uuid) from public, anon, authenticated;
grant execute on function public.admin_set_aetherion_maintenance(boolean, text, bigint, uuid) to service_role;
