-- Run once in Supabase Dashboard > SQL Editor.
-- New Discord accounts are inserted as pending. Only the account owner can read its row.
create table if not exists public.aetherion_access_requests (
    id uuid primary key references auth.users (id) on delete cascade,
    discord_name text not null default 'Discord user',
    status text not null default 'pending'
        check (status in ('pending', 'approved', 'rejected')),
    requested_at timestamptz not null default now(),
    reviewed_at timestamptz
);

alter table public.aetherion_access_requests enable row level security;
revoke all on table public.aetherion_access_requests from anon, authenticated;
grant select on table public.aetherion_access_requests to authenticated;

drop policy if exists "Users can read their own Aetherion access request"
    on public.aetherion_access_requests;
create policy "Users can read their own Aetherion access request"
    on public.aetherion_access_requests
    for select to authenticated
    using ((select auth.uid()) = id);

create or replace function public.create_aetherion_access_request()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.aetherion_access_requests (id, discord_name)
    values (
        new.id,
        coalesce(
            nullif(new.raw_user_meta_data ->> 'global_name', ''),
            nullif(new.raw_user_meta_data ->> 'full_name', ''),
            nullif(new.raw_user_meta_data ->> 'name', ''),
            nullif(new.raw_user_meta_data ->> 'preferred_username', ''),
            'Discord user'
        )
    )
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_aetherion_auth_user_created on auth.users;
create trigger on_aetherion_auth_user_created
    after insert on auth.users
    for each row execute procedure public.create_aetherion_access_request();

-- After reviewing a user in the Supabase Table Editor, set status to approved.
-- The app reads this status at sign-in; it never writes approval status itself.
