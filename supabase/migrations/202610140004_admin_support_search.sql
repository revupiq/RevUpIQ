BEGIN;

CREATE OR REPLACE FUNCTION public.search_admin_support_tickets(p_query text, p_status text DEFAULT 'open')
RETURNS TABLE(id uuid)
LANGUAGE sql
STABLE
SECURITY INVOKER
SET search_path = public
AS $$
    SELECT t.id
    FROM public.tickets t
    WHERE t.status IN ('open', 'closed')
      AND (p_status = 'all' OR t.status::text = p_status)
      AND (
          strpos(lower(coalesce(t.subject, '')), lower(btrim(p_query))) > 0
          OR EXISTS (
              SELECT 1 FROM public.profiles p
              WHERE p.id = t.user_id
                AND strpos(lower(coalesce(p.username, '')), lower(btrim(p_query))) > 0
          )
          OR EXISTS (
              SELECT 1 FROM public.ticket_messages m
              WHERE m.ticket_id = t.id
                AND strpos(lower(coalesce(m.body, '')), lower(btrim(p_query))) > 0
          )
      )
    ORDER BY t.updated_at DESC, t.id;
$$;

REVOKE ALL ON FUNCTION public.search_admin_support_tickets(text, text) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.search_admin_support_tickets(text, text) TO service_role;

COMMIT;
