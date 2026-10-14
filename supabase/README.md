# Division activation

Apply `migrations/202610140001_division_is_active.sql` to the Supabase database
before deploying this admin version. It adds `divisions.is_active`, defaulting
to true for existing and new divisions.

The admin keeps all divisions visible and allows deactivation and reactivation
from division details. Renaming and uploading images preserve the active state.
Deactivation does not delete divisions, associated content, or player history.

Apply `migrations/202610140002_hide_inactive_divisions.sql` to enforce visibility
in the backend. Row-level security allows `anon` and `authenticated` clients to
read only active divisions, including direct lookups by ID. The Flutter app uses
these roles and requires no code change or rebuild. Existing open screens refresh
when they next fetch division data.

The admin uses `service_role`, which bypasses row-level security, so inactive
divisions remain visible and editable there. Client roles have no division write
policies; division management is performed by the admin.

Player history and subscriptions are preserved. Player-side division metadata
lookups for inactive divisions return no record. This hides divisions; it does not
cancel subscriptions or independently block game/purchase RPCs or store billing.

Run `supabase db query --linked --file supabase/tests/division_visibility.sql` to
verify signed-out, signed-in, and admin visibility plus reactivation. Test data
changes are rolled back.

# Historical admin leaderboard

Apply `migrations/202610140003_admin_leaderboard_date.sql` before deploying the
updated leaderboard. The separate `get_admin_leaderboard` function is executable
only by the admin service role and preserves the existing Flutter RPC.

Daily results use the selected game date. Weekly and monthly results use the full
week (Monday through Sunday) or calendar month containing that date. The initial
date follows the existing game's midnight UTC-7 boundary. Points are the sum of
stored game scores for the selected period, mode, and division; fractional points
are displayed. Future dates are rejected by the admin endpoint.

Run `supabase db query --linked --file supabase/tests/admin_leaderboard_date.sql`
to compare historical scores against stored game results across game modes,
periods, and division filters, and verify RPC permissions.
