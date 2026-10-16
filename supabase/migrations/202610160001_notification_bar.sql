-- In-app notification bar: an admin posts a message (optionally with a
-- "Click Here" link) that every app user sees while it is active and inside
-- its on/off window. The app only ever reads rows it is allowed to show;
-- admin panel writes use the trusted service-role connection.
begin;

create table if not exists public.notification_bar (
  id uuid primary key default gen_random_uuid(),
  message text not null
    check (char_length(btrim(message)) between 1 and 500),
  link_url text
    check (link_url is null
           or (char_length(link_url) <= 2048 and link_url ~* '^https://[^[:space:]]+$')),
  -- Button text for the link; the app shows "Click Here" when empty
  link_label text
    check (link_label is null or char_length(btrim(link_label)) between 1 and 40),
  starts_at timestamptz not null,
  ends_at timestamptz not null,
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint notification_bar_window_check check (ends_at > starts_at)
);

create index if not exists notification_bar_live_idx
  on public.notification_bar (starts_at desc, ends_at)
  where is_active;

alter table public.notification_bar enable row level security;

-- Users only ever see what is live right now (server clock, not the phone's)
drop policy if exists notification_bar_public_read on public.notification_bar;
create policy notification_bar_public_read on public.notification_bar
  for select to anon, authenticated
  using (is_active and starts_at <= now() and now() < ends_at);

grant select on public.notification_bar to anon, authenticated;
revoke insert, update, delete, truncate on public.notification_bar from public, anon, authenticated;
grant all on public.notification_bar to service_role;

create or replace function public.set_notification_bar_updated_at()
returns trigger language plpgsql set search_path = '' as $$
begin
  new.updated_at := pg_catalog.now();
  return new;
end;
$$;
revoke execute on function public.set_notification_bar_updated_at() from public, anon, authenticated;

drop trigger if exists notification_bar_updated_at on public.notification_bar;
create trigger notification_bar_updated_at before update on public.notification_bar
  for each row execute function public.set_notification_bar_updated_at();

commit;
