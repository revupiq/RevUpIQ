BEGIN;

DO $$
DECLARE
    selected_date date;
    mode text;
    period text;
    division bigint;
    start_date date;
    end_date date;
BEGIN
    SELECT coalesce(max(played_date), current_date - 1) INTO selected_date
        FROM public.user_game_plays WHERE played_date < current_date;

    FOREACH mode IN ARRAY ARRAY[NULL::text, 'trivia', 'photoGuess', 'raceWord'] LOOP
        FOREACH period IN ARRAY ARRAY['daily', 'weekly', 'monthly'] LOOP
            FOR division IN SELECT NULL::bigint UNION ALL SELECT id FROM public.divisions LIMIT 2 LOOP
                start_date := CASE period
                    WHEN 'daily' THEN selected_date
                    WHEN 'weekly' THEN date_trunc('week', selected_date::timestamp)::date
                    ELSE date_trunc('month', selected_date::timestamp)::date END;
                end_date := CASE period
                    WHEN 'daily' THEN selected_date + 1
                    WHEN 'weekly' THEN start_date + 7
                    ELSE (start_date + interval '1 month')::date END;

                IF EXISTS (
                    WITH expected AS (
                        SELECT ugp.user_id, sum(ugp.score)::float8 AS total_score, count(*)::int AS games_played
                        FROM public.user_game_plays ugp
                        WHERE ugp.played_date >= start_date AND ugp.played_date < end_date
                          AND (mode IS NULL OR ugp.game_type::text = mode)
                          AND (division IS NULL OR ugp.division_id = division)
                        GROUP BY ugp.user_id
                    ), actual AS (
                        SELECT * FROM public.get_admin_leaderboard(selected_date, mode, division, period)
                    )
                    (SELECT * FROM expected EXCEPT SELECT * FROM actual)
                    UNION ALL
                    (SELECT * FROM actual EXCEPT SELECT * FROM expected)
                ) THEN
                    RAISE EXCEPTION 'Historical scores mismatch for %, %, %', selected_date, mode, period;
                END IF;
            END LOOP;
        END LOOP;
    END LOOP;

    IF has_function_privilege('anon', 'public.get_admin_leaderboard(date,text,bigint,text)', 'EXECUTE')
       OR has_function_privilege('authenticated', 'public.get_admin_leaderboard(date,text,bigint,text)', 'EXECUTE')
       OR NOT has_function_privilege('service_role', 'public.get_admin_leaderboard(date,text,bigint,text)', 'EXECUTE') THEN
        RAISE EXCEPTION 'Admin leaderboard permissions mismatch';
    END IF;
END $$;

ROLLBACK;
