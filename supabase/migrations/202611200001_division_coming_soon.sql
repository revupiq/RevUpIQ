-- An inactive division can still show on the app's subscription page, marked
-- "Coming Soon" instead of its price and Subscribe button. Once the division is
-- activated it shows normally, whatever coming_soon says.
--
-- Players still can't read inactive divisions (divisions_read_active), so the
-- lobby, profile, leaderboards and everything else stay as they are. The
-- subscription page gets the coming-soon ones from coming_soon_divisions(),
-- which returns only what the card shows (no store product IDs).
begin;

alter table public.divisions
  add column if not exists coming_soon boolean not null default false;

create or replace function public.coming_soon_divisions()
returns table (id bigint, name text, logo_url text, purchase_icon_url text, sort_index integer)
language sql stable security definer set search_path = '' as $$
  select d.id, d.name, d.logo_url, d.purchase_icon_url, d.sort_index
  from public.divisions d
  where not d.is_active and d.coming_soon
  order by d.sort_index, d.id;
$$;

revoke all on function public.coming_soon_divisions() from public;
grant execute on function public.coming_soon_divisions() to anon, authenticated;

commit;
