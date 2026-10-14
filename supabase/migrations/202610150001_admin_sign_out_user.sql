-- Ends every session of a user so a ban takes effect on devices that are already
-- signed in (banned_until alone only blocks new sign-ins). Called by the admin
-- panel with the service role when it bans a user.
begin;

create or replace function public.admin_sign_out_user(p_user_id uuid)
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_sessions integer;
begin
  delete from auth.refresh_tokens where user_id = p_user_id::text;

  with removed as (
    delete from auth.sessions where user_id = p_user_id returning 1
  )
  select count(*) into v_sessions from removed;

  return v_sessions;
end;
$$;

revoke all on function public.admin_sign_out_user(uuid) from public, anon, authenticated;
grant execute on function public.admin_sign_out_user(uuid) to service_role;

commit;
