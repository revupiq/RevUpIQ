-- Run with `supabase db query --linked --file supabase/tests/division_visibility.sql`.
-- All test changes are rolled back; no division is deactivated permanently.
BEGIN;

DO $$
DECLARE
    test_id integer;
BEGIN
    SELECT id INTO test_id FROM public.divisions WHERE is_active ORDER BY id LIMIT 1;
    IF test_id IS NULL THEN
        RAISE EXCEPTION 'An active division is required for this visibility check';
    END IF;
    PERFORM set_config('revupiq.test_division_id', test_id::text, true);
    UPDATE public.divisions SET is_active = false WHERE id = test_id;
    PERFORM set_config('revupiq.expected_visible',
        (SELECT count(*)::text FROM public.divisions WHERE is_active), true);
END $$;

SET LOCAL ROLE anon;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.divisions WHERE NOT is_active)
       OR (SELECT count(*) FROM public.divisions) <> current_setting('revupiq.expected_visible')::integer THEN
        RAISE EXCEPTION 'Signed-out division visibility failed';
    END IF;
END $$;

RESET ROLE;
SET LOCAL ROLE authenticated;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.divisions WHERE NOT is_active)
       OR (SELECT count(*) FROM public.divisions) <> current_setting('revupiq.expected_visible')::integer THEN
        RAISE EXCEPTION 'Signed-in division visibility failed';
    END IF;
END $$;

RESET ROLE;
SET LOCAL ROLE service_role;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.divisions
                   WHERE id = current_setting('revupiq.test_division_id')::integer AND NOT is_active) THEN
        RAISE EXCEPTION 'Admin cannot see the inactive division';
    END IF;
    UPDATE public.divisions SET is_active = true
        WHERE id = current_setting('revupiq.test_division_id')::integer;
END $$;

RESET ROLE;
SET LOCAL ROLE authenticated;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.divisions
                   WHERE id = current_setting('revupiq.test_division_id')::integer) THEN
        RAISE EXCEPTION 'Reactivated division is not visible';
    END IF;
END $$;

RESET ROLE;
ROLLBACK;
