BEGIN;
DO $$
DECLARE
    sample record;
BEGIN
    SELECT t.id, t.status, p.username INTO sample
    FROM public.tickets t JOIN public.profiles p ON p.id = t.user_id
    WHERE t.status IN ('open', 'closed') AND coalesce(p.username, '') <> '' LIMIT 1;
    IF FOUND AND NOT EXISTS (
        SELECT 1 FROM public.search_admin_support_tickets(upper(sample.username), 'all') s WHERE s.id = sample.id
    ) THEN
        RAISE EXCEPTION 'Username search failed';
    END IF;

    SELECT id, status, subject INTO sample FROM public.tickets
    WHERE status IN ('open', 'closed') AND coalesce(subject, '') <> '' LIMIT 1;
    IF FOUND AND NOT EXISTS (
        SELECT 1 FROM public.search_admin_support_tickets(upper(sample.subject), sample.status::text) s WHERE s.id = sample.id
    ) THEN
        RAISE EXCEPTION 'Subject search failed';
    END IF;

    SELECT m.ticket_id AS id, left(m.body, 30) AS word INTO sample
    FROM public.ticket_messages m JOIN public.tickets t ON t.id = m.ticket_id
    WHERE t.status IN ('open', 'closed') AND coalesce(m.body, '') <> '' LIMIT 1;
    IF FOUND AND NOT EXISTS (
        SELECT 1 FROM public.search_admin_support_tickets(sample.word, 'all') s WHERE s.id = sample.id
    ) THEN
        RAISE EXCEPTION 'Message text search failed';
    END IF;

    IF EXISTS (SELECT 1 FROM public.search_admin_support_tickets('', 'open') s
               JOIN public.tickets t ON t.id = s.id WHERE t.status <> 'open')
       OR EXISTS (SELECT 1 FROM public.search_admin_support_tickets('', 'closed') s
                  JOIN public.tickets t ON t.id = s.id WHERE t.status <> 'closed') THEN
        RAISE EXCEPTION 'Status filter failed';
    END IF;

    IF EXISTS (SELECT 1 FROM public.search_admin_support_tickets('__no_match_' || gen_random_uuid()::text, 'all')) THEN
        RAISE EXCEPTION 'Empty result check failed';
    END IF;

    IF has_function_privilege('anon', 'public.search_admin_support_tickets(text,text)', 'EXECUTE')
       OR has_function_privilege('authenticated', 'public.search_admin_support_tickets(text,text)', 'EXECUTE')
       OR NOT has_function_privilege('service_role', 'public.search_admin_support_tickets(text,text)', 'EXECUTE') THEN
        RAISE EXCEPTION 'Search access permissions failed';
    END IF;
END $$;
ROLLBACK;
