# Admin changes to preserve during the GitHub merge

Recorded on October 14, 2026. This checklist covers work completed in this chat.
It is a merge reference, not a replacement for the other agent's changes.

## Divisions

- Division details have Deactivate Division / Reactivate Division controls.
- Inactive divisions remain visible in the admin, with an inactive label.
- Deactivation preserves content, subscriptions, and history.
- `DivisionDto.IsActive` maps to `divisions.is_active`, defaulting to true.
- `DivisionsController.SetActive` saves only the activation flag and validates the
  antiforgery token.
- Backend row-level security hides inactive divisions from Flutter's `anon` and
  `authenticated` roles, including direct division lookups. Admin service-role
  access can still see and reactivate them. No Flutter source change was needed.
- Hiding does not cancel billing or independently block game/purchase RPCs.

Important files: `Controllers/DivisionsController.cs`,
`Models/Division/DivisionDto.cs`, `Views/Divisions/_EditDivisionModal.cshtml`,
`Views/Divisions/_Index.cshtml`, `wwwroot/css/ui.css`.

## Leaderboard

- Points column displays the total for the selected date period, game mode, and
  division; fractional points are preserved.
- Date selector supports previous days. Weekly/monthly select the full week or
  month containing the date. Weeks start Monday.
- Initial day follows the game's midnight UTC-7 boundary. Future dates are rejected.
- All Games and All divisions are available; results load when the page opens.
- Changing dropdowns/dates loads new results. Older responses cannot replace the
  newest selection; loading and failure messages are displayed.
- Filter layout adapts to narrow screens.
- The separate admin-only `get_admin_leaderboard` RPC preserves Flutter's existing
  `get_leaderboard` contract.
- Leaderboard controller requires admin sign-in.

Important files: `Controllers/LeaderboardController.cs`,
`Models/Leaderboard/LeaderboardEntryViewModel.cs`, `Views/Leaderboard/`,
`wwwroot/js/leaderboard.js`, `wwwroot/css/leaderboard.css`.

## Users list

- Username column uses the profile's username, including search results.
- Existing batched profile queries fetch `id,username`; missing usernames show a dash.
- Search cells use text content, so usernames cannot inject markup.
- Search requests cancel older requests to avoid stale responses.
- Desktop columns resize and long values wrap. Below 700px, rows become cards with
  field labels. No horizontal scrollbar; hover does not enlarge rows.
- Layout rules are scoped to the users list to preserve other shared user views.

Important files: `Controllers/UsersController.cs`, `Models/Users/AuthUserDto.cs`,
`Views/Users/UsersList.cshtml`, `wwwroot/css/users.css`.

## Display timezone

- All displayed timestamps use fixed UTC-7 throughout the year.
- Last Login, account/profile creation and updates, subscription timestamps, ban
  expiry, support tickets/messages, suggestions, and sponsor requests are converted.
- Server-rendered and browser-rendered timestamps follow the same conversion.
- UTC timestamps without an explicit timezone are interpreted as UTC.
- Missing last-login values show Never. Date rollover is handled correctly.
- Calendar defaults and today's highlighting use UTC-7. Leaderboard uses the same helper.
- Birthdays, league date-only ranges, selected game dates, stored UTC timestamps,
  and support polling cursors keep their original meanings.
- Pages display the notice: All timestamps are shown in UTC-7.

Important files: `Infrastructure/DisplayTime.cs`, `wwwroot/js/admin-time.js`,
`Views/_ViewImports.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/Users/`,
`Views/Support/`, `Views/Exrta/`, `Views/Sponsor/`, `Views/MiniGames/_Calendar.cshtml`,
`Views/MiniGames/_DailyGameFilter.cshtml`, `Controllers/MiniGamesController.cs`,
`wwwroot/js/calendar.js`, `wwwroot/js/dailyGameFilter.js`.

## Support history on user pages

- At the bottom of user details, Support tickets lists only that user's tickets.
- Open and previous tickets have separate headings and counts; newest updates
  appear first within each group.
- Each ticket shows subject, status, created/updated timestamps in UTC-7, and a
  link to the existing support ticket page.
- Empty and failed-load states are distinct; failure includes a retry link.
- Ticket loading happens only for UserDetails, not every shared profile load.
- UserDetails requires admin sign-in before displaying private support history.
- Ticket cards adapt to smaller screens.

Important files: `Controllers/UsersController.cs`,
`Models/Users/UserProfileViewModel.cs`, `Views/Users/User.cshtml`,
`Views/Users/_UserSupportTickets.cshtml`, `wwwroot/css/user-support-tickets.css`.

## Database changes already applied to the live Supabase project

These files must be included in Git even though the database already has the changes:

1. `supabase/migrations/202610140001_division_is_active.sql`
2. `supabase/migrations/202610140002_hide_inactive_divisions.sql`
3. `supabase/migrations/202610140003_admin_leaderboard_date.sql`

Keep `supabase/README.md`, `supabase/.gitignore`,
`supabase/tests/division_visibility.sql`, and
`supabase/tests/admin_leaderboard_date.sql` too. The `.temp` directory contains
local CLI state and is ignored. Do not blindly replay already-applied migrations
on the live database outside the project's normal migration tracking process.

## Checks completed and checks to repeat after merging

- Builds passed; the repository has existing nullable-reference warnings.
- Division visibility and reactivation passed checks for anon, authenticated, and
  service-role access. Temporary test changes were rolled back.
- Historical leaderboard totals matched stored game results across modes, periods,
  and division filters; admin RPC permissions were checked.
- Previous-day leaderboard and fractional points were verified in the browser.
- Username appeared in normal and search rows.
- Users list had no horizontal overflow at desktop, tablet, and phone sizes.
- Six server and six browser timestamp checks passed, covering missing values,
  explicit offsets, unspecified UTC, and day/year rollover.
- User details displayed two existing closed tickets; signed-out requests redirected
  to login. No support tickets were created or changed for verification.

After merging, rebuild and restart the local app, then repeat the above UI checks.
At the time of this note, the local app was running on `http://localhost:5198/`
using the Release build with the Development launch profile. Windows had locked
some Debug build files, so Release was used for local verification.

Some changes may already be committed; several others were still modified or
untracked when this note was written. Include new files as well as tracked diffs.

## Merge verification on October 14, 2026

Compared the saved source backup with commit `2155fbb` (Combine main with the
in-progress local work). All 148 backed-up files remained present. Six files had
changed to incorporate user-management additions; the features recorded above
were preserved. Division-visibility and historical-leaderboard database checks
passed again, and the merged project built successfully.

The new profile badge inherited a mobile Username label from the users-list CSS.
Corrected the selectors to target direct row cells, preserving the new profile
and delete columns. Browser checks confirmed the badge label was removed and
there was no horizontal overflow on a phone viewport.

At the end of this verification, this checklist was still untracked, and the small
`wwwroot/css/users.css` correction was an uncommitted change. Include both in the
next commit so they reach GitHub.

Support timestamp follow-up: ticket-list and message timestamps now explicitly
display `(UTC-7)` beside the converted time, including messages loaded dynamically.
Verified an existing message stored at 16:07 UTC displays as 09:07 UTC-7.
Preserve these changes in `Views/Support/Index.cshtml` and `Views/Support/Ticket.cshtml`.

## Support inbox, closed tickets, and search follow-up

- `/Support` defaults to open tickets. Closed tickets remain accessible at
  `/Support/Closed`; they are excluded from the active inbox after closing.
- Open / Closed / All tickets dropdown preserves the search term. All tickets
  uses `/Support/All` so both statuses can be searched together.
- Search matches usernames, ticket subjects, and message text, ignoring case.
  Search characters are treated literally rather than as wildcard operators.
- Redesigned layout has a search toolbar, status badges, ticket counts, ticket
  cards, distinct empty/no-match messages, and collapsible support settings.
- Warning/category controls remain available in Support settings on every list.
- Ticket details link back to Active or Closed Tickets according to their status.
- Support requires admin sign-in. The search RPC is service-role-only.
- Backend checks passed for username/subject/message search, status separation,
  no-match behavior, and permissions. Browser checks passed for the dropdown,
  username and message search, settings loading, and phone horizontal overflow.

Preserve `Controllers/SupportController.cs`,
`Models/Support/SupportTicketsIndexViewModel.cs`, `Views/Support/Index.cshtml`,
`Views/Support/Ticket.cshtml`, `wwwroot/css/support-tickets.css`,
`supabase/migrations/202610140004_admin_support_search.sql`, and
`supabase/tests/admin_support_search.sql`. The search migration has already been
applied to the live Supabase project. Email notifications were explicitly skipped
and are not implemented.

Support ticket back navigation follow-up: `Views/Support/Ticket.cshtml` now uses
the `.stx-back` button styling in `wwwroot/css/support.css`, with an aligned page
heading, visible focus treatment, and consistent normal/visited colors. Verified
that the closed-ticket button points to `/Support/Closed` and has no underline.

Email notifications have been requested again with `info@revupiq.app` as the
recipient, but are not enabled. An outbound email provider or SMTP configuration
and approved sender address are still required.

## Flutter RaceWord suggestion follow-up

The user chose to match the admin's 4, 5, or 6-letter rules. In the separate
`I:/Freelancing/MotorMindIQ/flutter_motormind_iq` repository:

- `lib/features/challenge_submission/race_word_submission_page.dart` now caps
  input at six characters, shows `Min 4 and Max 6 Characters`, marks the field
  required, and enables submission for 4-6 alphabetic letters. The previous page
  capped input at five but required six to enable submission.
- `lib/API/challenge_submission_service.dart` validates before database writes
  and normalizes accepted words to uppercase.
- `test/race_word_submission_test.dart` verifies required/invalid input,
  acceptance of 4/5/6 letters, six-character capping, and submitted normalization.
- Both focused tests passed and targeted Flutter analysis reported no issues.
- The existing `race_word_submissions.word` database column already allows six
  characters, so no schema change was necessary.

These Flutter changes must be committed in the Flutter repository separately.
An app rebuild or hot reload is required to display them; updating the admin
server does not update installed Flutter apps. Email notifications remain skipped.

RaceWord Submit follow-up: rechecked the same Flutter form and reran both focused
tests successfully. Attached to the existing Android emulator, completed a hot
reload, and detached without terminating the app. The original five-character cap
versus six-character validation conflict is removed; valid 4-6-letter suggestions
enable Submit.
