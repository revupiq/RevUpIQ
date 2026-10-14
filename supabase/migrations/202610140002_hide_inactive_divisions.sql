BEGIN;

ALTER TABLE public.divisions ENABLE ROW LEVEL SECURITY;

-- Flutter uses anon/authenticated; the admin's service_role bypasses RLS.
CREATE POLICY divisions_read_active
    ON public.divisions
    FOR SELECT
    TO anon, authenticated
    USING (is_active = true);

COMMIT;
