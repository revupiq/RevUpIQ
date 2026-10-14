-- Sponsor ads per division.
-- division_id NULL = shown in every division (the existing behaviour).
-- A division-specific ad for a week wins over the "all divisions" ad.
begin;

alter table public.sponsor_bar
    add column if not exists division_id bigint
    references public.divisions(id) on delete cascade;

-- The old rule allowed only one ad per week in total. Replace it with one ad per
-- week per division (and one "all divisions" ad per week).
alter table public.sponsor_bar
    drop constraint if exists sponsor_bar_year_week_unique;
drop index if exists public.sponsor_bar_year_week_unique;

create unique index if not exists sponsor_bar_year_week_division_key
    on public.sponsor_bar (year, week_number, coalesce(division_id, 0));

-- Existing app versions call this with only (p_year, p_week); the new optional
-- p_division_id keeps that working (they get the "all divisions" ad).
drop function if exists public.get_active_sponsor_bar_id(integer, integer);

create or replace function public.get_active_sponsor_bar_id(
    p_year integer,
    p_week integer,
    p_division_id bigint default null
)
returns uuid
language sql
stable
as $$
    select sponsor_id
    from public.sponsor_bar
    where year = p_year
      and week_number = p_week
      and (division_id is null or division_id = p_division_id)
    order by (division_id is null) asc   -- division-specific first
    limit 1;
$$;

-- Admin calendar: which weeks already have an ad for exactly this scope
drop function if exists public.get_sponsor_weeks_status(integer);

create or replace function public.get_sponsor_weeks_status(
    p_year integer,
    p_division_id bigint default null
)
returns table(week_number integer, has_data boolean)
language sql
stable
as $$
  with weeks as (
    select generate_series(1, 53) as week_number
  )
  select
    w.week_number,
    exists (
      select 1
      from public.sponsor_bar sb
      where sb.year = p_year
        and sb.week_number = w.week_number
        and sb.division_id is not distinct from p_division_id
    ) as has_data
  from weeks w
  where w.week_number <= (
    extract(week from make_date(p_year, 12, 28))::int
  )
  order by w.week_number;
$$;

commit;
