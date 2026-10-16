BEGIN;
DO $$
DECLARE
    live uuid;
    inactive uuid;
    future uuid;
    ended uuid;
    seen int;
BEGIN
    INSERT INTO public.notification_bar (message, starts_at, ends_at)
    VALUES ('live', now() - interval '1 hour', now() + interval '1 hour') RETURNING id INTO live;
    INSERT INTO public.notification_bar (message, starts_at, ends_at, is_active)
    VALUES ('inactive', now() - interval '1 hour', now() + interval '1 hour', false) RETURNING id INTO inactive;
    INSERT INTO public.notification_bar (message, starts_at, ends_at)
    VALUES ('future', now() + interval '1 hour', now() + interval '2 hours') RETURNING id INTO future;
    INSERT INTO public.notification_bar (message, starts_at, ends_at)
    VALUES ('ended', now() - interval '2 hours', now() - interval '1 hour') RETURNING id INTO ended;

    SET LOCAL ROLE authenticated;

    SELECT count(*) INTO seen FROM public.notification_bar WHERE id = live;
    IF seen <> 1 THEN
        RAISE EXCEPTION 'Live notification is hidden from users';
    END IF;

    SELECT count(*) INTO seen FROM public.notification_bar WHERE id IN (inactive, future, ended);
    IF seen <> 0 THEN
        RAISE EXCEPTION 'Inactive or out-of-window notification is visible to users';
    END IF;

    RESET ROLE;

    IF has_table_privilege('anon', 'public.notification_bar', 'INSERT')
       OR has_table_privilege('authenticated', 'public.notification_bar', 'INSERT')
       OR has_table_privilege('authenticated', 'public.notification_bar', 'UPDATE')
       OR has_table_privilege('authenticated', 'public.notification_bar', 'DELETE')
       OR NOT has_table_privilege('service_role', 'public.notification_bar', 'INSERT')
       OR NOT has_table_privilege('authenticated', 'public.notification_bar', 'SELECT') THEN
        RAISE EXCEPTION 'Notification bar permissions failed';
    END IF;

    BEGIN
        INSERT INTO public.notification_bar (message, starts_at, ends_at)
        VALUES ('backwards', now(), now() - interval '1 minute');
        RAISE EXCEPTION 'Off date before on date was accepted';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;

    BEGIN
        INSERT INTO public.notification_bar (message, link_url, starts_at, ends_at)
        VALUES ('bad link', 'javascript:alert(1)', now(), now() + interval '1 hour');
        RAISE EXCEPTION 'Non-https link was accepted';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;
END $$;
ROLLBACK;
