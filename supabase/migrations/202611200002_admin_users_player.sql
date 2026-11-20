-- An admin account can belong to a player who already has an app account: the
-- owner picks the player on the Admin Users page. The admin login keeps its own
-- username and password (players sign in to the app with phone codes, Google or
-- Apple, which the panel doesn't use). Deleting the player keeps the admin
-- account and only removes the link. One admin account per player.
begin;

alter table public.admin_users
  add column if not exists user_id uuid references auth.users (id) on delete set null;

create unique index if not exists admin_users_user_id_key
  on public.admin_users (user_id) where user_id is not null;

commit;
