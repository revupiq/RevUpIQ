-- Existing and newly created divisions remain active by default.
ALTER TABLE public.divisions
    ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT true;
