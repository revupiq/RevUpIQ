BEGIN;

-- Separate admin endpoint preserves the existing Flutter leaderboard contract.
CREATE OR REPLACE FUNCTION public.get_admin_leaderboard(
    p_date date,
    p_game_type text DEFAULT NULL,
    p_division_id bigint DEFAULT NULL,
    p_time_filter text DEFAULT 'daily'
)
RETURNS TABLE(user_id uuid, total_score double precision, games_played integer)
LANGUAGE sql
STABLE
SECURITY INVOKER
SET search_path = public
AS $$
    SELECT ugp.user_id,
           sum(ugp.score)::float8 AS total_score,
           count(*)::int AS games_played
    FROM public.user_game_plays ugp
    WHERE (p_game_type IS NULL OR ugp.game_type::text = p_game_type)
      AND (p_division_id IS NULL OR ugp.division_id = p_division_id)
      AND ugp.played_date >= CASE p_time_filter
          WHEN 'daily' THEN p_date
          WHEN 'weekly' THEN date_trunc('week', p_date::timestamp)::date
          WHEN 'monthly' THEN date_trunc('month', p_date::timestamp)::date
      END
      AND ugp.played_date < CASE p_time_filter
          WHEN 'daily' THEN p_date + 1
          WHEN 'weekly' THEN date_trunc('week', p_date::timestamp)::date + 7
          WHEN 'monthly' THEN (date_trunc('month', p_date::timestamp) + interval '1 month')::date
      END
    GROUP BY ugp.user_id
    ORDER BY total_score DESC, ugp.user_id;
$$;

REVOKE ALL ON FUNCTION public.get_admin_leaderboard(date, text, bigint, text)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.get_admin_leaderboard(date, text, bigint, text)
    TO service_role;

COMMIT;
