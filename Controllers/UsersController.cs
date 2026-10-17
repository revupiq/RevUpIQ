using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.Users;


namespace RevUpIQ.Admin.Controllers
{
    public class UsersController : Controller
    {
        private readonly Supabase.Client _supabase;

        public UsersController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }


        public async Task<IActionResult> UsersList(int page = 1, int perPage = 50, string profile = "all")
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"UsersList called. Page={page}, PerPage={perPage}, Profile={profile}");
            Console.ResetColor();

            profile = NormalizeProfileFilter(profile);

            List<AuthUser> users = new();
            bool hasNextPage;

            using var http = new HttpClient();
            http.BaseAddress = new Uri(ApiClientFactory.BaseUrl);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);

            try
            {
                if (profile == "all")
                {
                    users = await FetchAuthUsersPageAsync(http, page, perPage);
                    hasNextPage = users.Count == perPage;
                    await MarkProfilesAsync(users);
                    await MarkSubscriptionsAsync(users);
                }
                else
                {
                    // Filtering needs every user, so load them all and page in memory
                    var all = new List<AuthUser>();
                    const int chunk = 1000;
                    for (var p = 1; ; p++)
                    {
                        var batch = await FetchAuthUsersPageAsync(http, p, chunk);
                        all.AddRange(batch);
                        if (batch.Count < chunk) break;
                    }

                    await MarkProfilesAsync(all);

                    var wantProfile = profile == "with";
                    var filtered = all.Where(u => u.HasProfile == wantProfile).ToList();

                    users = filtered.Skip((page - 1) * perPage).Take(perPage).ToList();
                    hasNextPage = filtered.Count > page * perPage;
                    await MarkSubscriptionsAsync(users);
                }
            }
            catch (Exception ex)
            {
                hasNextPage = false;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Supabase failed — {ex.Message}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Users fetched: {users.Count}");
            Console.ResetColor();

            var vm = new UsersListViewModel
            {
                Users = users,
                Page = page,
                PerPage = perPage,
                HasNextPage = hasNextPage,
                Profile = profile
            };

            return View(vm);
        }

        private static string NormalizeProfileFilter(string? profile) =>
            (profile ?? "").Trim().ToLowerInvariant() switch
            {
                "with" => "with",
                "without" => "without",
                _ => "all"
            };

        private static async Task<List<AuthUser>> FetchAuthUsersPageAsync(HttpClient http, int page, int perPage)
        {
            var response = await http.GetAsync($"auth/v1/admin/users?page={page}&per_page={perPage}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<AuthUsersResponse>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return data?.Users ?? new List<AuthUser>();
        }

        // Sets HasProfile on each user by checking which ids exist in the profiles table
        private async Task MarkProfilesAsync(List<AuthUser> users)
        {
            var guids = users
                .Where(u => Guid.TryParse(u.Id, out _))
                .Select(u => Guid.Parse(u.Id))
                .ToList();

            var profileUsernames = new Dictionary<Guid, string?>();

            foreach (var group in guids.Chunk(100))
            {
                var result = await _supabase
                    .From<Profile>()
                    .Select("id,username")
                    .Filter("id", Supabase.Postgrest.Constants.Operator.In, group.ToList())
                    .Get();

                foreach (var p in result.Models)
                    profileUsernames[p.Id] = p.Username;
            }

            foreach (var u in users)
            {
                var hasId = Guid.TryParse(u.Id, out var id);
                u.HasProfile = hasId && profileUsernames.ContainsKey(id);
                if (u.HasProfile)
                    u.Username = profileUsernames[id];
            }
        }

        // Sets SubscriptionStatus on each user from their user_divisions rows
        private async Task MarkSubscriptionsAsync(List<AuthUser> users)
        {
            var guids = users
                .Where(u => Guid.TryParse(u.Id, out _))
                .Select(u => Guid.Parse(u.Id))
                .ToList();

            var byUser = new Dictionary<Guid, List<UserDivision>>();

            foreach (var group in guids.Chunk(100))
            {
                var result = await _supabase
                    .From<UserDivision>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.In, group.ToList())
                    .Get();

                foreach (var d in result.Models)
                {
                    if (!byUser.TryGetValue(d.User_Id, out var list))
                        byUser[d.User_Id] = list = new List<UserDivision>();
                    list.Add(d);
                }
            }

            var now = DateTime.UtcNow;
            foreach (var u in users)
            {
                var rows = (Guid.TryParse(u.Id, out var id) ? byUser.GetValueOrDefault(id) : null)
                    ?? new List<UserDivision>();
                u.SubscriptionStatus = SubscriptionStatus.Label(SubscriptionStatus.ForUser(rows, now));
            }
        }

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UserDetails(string id)
        {
            var vm = await LoadUserProfileAsync(id);
            if (vm?.Profile != null)
            {
                try
                {
                    var tickets = await _supabase.From<YourApp.Models.Support.Ticket>()
                        .Where(t => t.UserId == vm.Profile.Id)
                        .Order("updated_at", Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get();
                    vm.SupportTickets = tickets.Models.ToList();
                }
                catch (Exception ex)
                {
                    vm.SupportTicketsLoadFailed = true;
                    HttpContext.RequestServices.GetRequiredService<ILogger<UsersController>>()
                        .LogError(ex, "Failed to load support tickets for {UserId}", vm.Profile.Id);
                }
            }

            ViewBag.DefaultCoverUrl = await AppSettingsClient.GetAsync(AppSettingsClient.DefaultCoverKey);
            return View("User", vm);
        }

        [HttpGet]
        public async Task<IActionResult> UserDivision(long id)
        {
            var udResult = await _supabase
                .From<UserDivision>()
                .Where(x => x.Id == id)
                .Get();

            var ud = udResult.Models.FirstOrDefault();
            if (ud == null)
                return Content(string.Empty);

            var divResult = await _supabase
                .From<RevUpIQ.Admin.Models.Division.DivisionDto>()
                .Where(d => d.Id == ud.Division_Id)
                .Get();

            var vm = new UserDivisionViewModel
            {
                UserDivision = ud,
                Division = divResult.Models.FirstOrDefault()
            };

            return PartialView("_UserDivision", vm);
        }

        [HttpGet]
        public async Task<IActionResult> UserGameHistory(string id, int? divisionId = null)
        {
            if (!Guid.TryParse(id, out var userId))
                return Content(string.Empty);

            var games = await GetUserGamePlaysAsync(_supabase, userId);

            if (divisionId.HasValue)
                games = games.Where(g => g.Division_Id == divisionId.Value).ToList();

            var divisionsResult = await _supabase
                .From<RevUpIQ.Admin.Models.Division.DivisionDto>()
                .Get();

            var vm = new UserGameHistoryViewModel
            {
                Games = games,
                Divisions = divisionsResult.Models.ToList()
            };

            ViewBag.SelectedDivisionId = divisionId;

            Console.WriteLine($"[UserGameHistory] SelectedDivisionId = {(divisionId.HasValue ? divisionId.Value.ToString() : "ALL")}");

            return PartialView("_UserGameHistory", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Profile model)
        {
            System.Diagnostics.Debug.WriteLine("UpdateProfile called");

            if (model.Id == Guid.Empty)
                return BadRequest();

            await _supabase
                .From<Profile>()
                .Where(p => p.Id == model.Id)
                .Set(p => p.Username!, model.Username)
                .Set(p => p.Full_Name!, model.Full_Name)
                .Set(p => p.Country!, model.Country)
                .Set(p => p.State!, model.State)
                .Set(p => p.Birthday!, model.Birthday)
                .Set(p => p.Sex!, model.Sex)
                .Set(p => p.Description!, model.Description)
                .Set(p => p.Updated_At, DateTime.UtcNow)
                .Update();

            return Ok();
        }


        [HttpGet]
        public async Task<IActionResult> UserProfile(string id)
        {
            var vm = await LoadUserProfileAsync(id);
            if (vm == null) return Content(string.Empty);

            return PartialView("_UserProfile", vm);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BanUser([FromBody] BanUserRequest request)
        {
            Console.WriteLine("BanUser called");

            if (request == null || string.IsNullOrWhiteSpace(request.UserId) || request.Duration <= 0)
                return BadRequest();

            var unit = (request.Unit ?? "").Trim().ToLowerInvariant();
            var hours = unit == "days" || unit == "day" || unit == "d"
                ? checked(request.Duration * 24)
                : request.Duration;

            var duration = $"{hours}h";

            var url = $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/auth/v1/admin/users/{request.UserId}";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);

            var payload = System.Text.Json.JsonSerializer.Serialize(new { ban_duration = duration });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var res = await http.PutAsync(url, content);
            var body = await res.Content.ReadAsStringAsync();

            Console.WriteLine($"BanUser response: {(int)res.StatusCode} {res.ReasonPhrase}");
            Console.WriteLine(body);

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, body);

            // banned_until only blocks new sign-ins. End the user's existing sessions so
            // a player who is already signed in is cut off too.
            var signedOut = await SignOutUserAsync(http, request.UserId);

            return Ok(new { signedOut });
        }

        private static async Task<bool> SignOutUserAsync(HttpClient http, string userId)
        {
            if (!Guid.TryParse(userId, out var id))
                return false;

            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new { p_user_id = id });
                using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                var res = await http.PostAsync(
                    $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/rest/v1/rpc/admin_sign_out_user", content);

                Console.WriteLine($"admin_sign_out_user response: {(int)res.StatusCode}");
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"admin_sign_out_user failed: {ex.Message}");
                return false;
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UnbanUser([FromBody] BanUserRequest request)
        {
            Console.WriteLine("UnbanUser called");

            if (request == null || string.IsNullOrWhiteSpace(request.UserId))
                return BadRequest();

            var url = $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/auth/v1/admin/users/{request.UserId}";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                ban_duration = "0h"
            });

            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var res = await http.PutAsync(url, content);
            var body = await res.Content.ReadAsStringAsync();

            Console.WriteLine($"UnbanUser response: {(int)res.StatusCode} {res.ReasonPhrase}");
            Console.WriteLine(body);

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, body);

            return Ok();
        }

        public async Task<UserProfileViewModel?> LoadUserProfileAsync(string id)
        {
            if (!Guid.TryParse(id, out var userId))
                return null;

            var profileResult = await _supabase
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Get();

            var profile = profileResult.Models.FirstOrDefault();
            if (profile == null)
                return null;

            AuthUser? authUser = null;

            try
            {
                using var http = new HttpClient();
                http.BaseAddress = new Uri(ApiClientFactory.BaseUrl);
                http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);

                var response = await http.GetAsync($"auth/v1/admin/users/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    authUser = System.Text.Json.JsonSerializer.Deserialize<AuthUser>(
                        json,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
                        });
                }
            }
            catch
            {
            }

            var games = await GetUserGamePlaysAsync(_supabase, userId);
            var divisions = await GetUserDivisionsAsync(_supabase, userId);

            return new UserProfileViewModel
            {
                Profile = profile,
                AuthUser = authUser,
                GamePlays = games,
                Divisions = divisions
            };
        }

        public static async Task<List<UserDivision>> GetUserDivisionsAsync(Supabase.Client supabase, Guid userId)
        {
            var result = await supabase
                .From<UserDivision>()
                .Where(d => d.User_Id == userId)
                .Get();

            return result.Models.ToList();
        }
        public static async Task<List<UserGamePlay>> GetUserGamePlaysAsync(Supabase.Client supabase, Guid userId)
        {
            var result = await supabase
                .From<UserGamePlay>()
                .Where(g => g.User_Id == userId)
                .Get();

            return result.Models.ToList();
        }
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string q, string profile = "all")
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new List<AuthUser>());

            var ids = await SearchUserIds(q);
            var usernames = new Dictionary<Guid, string?>();

            // Usernames for the results (users without a profile have none)
            if (ids.Any())
            {
                try
                {
                    var profileResult = await _supabase
                        .From<Profile>()
                        .Select("id,username")
                        .Filter("id", Supabase.Postgrest.Constants.Operator.In, ids)
                        .Get();

                    usernames = profileResult.Models.ToDictionary(p => p.Id, p => p.Username);
                }
                catch { }
            }

            var users = new List<AuthUser>();

            using var http = new HttpClient();
            http.BaseAddress = new Uri(ApiClientFactory.BaseUrl);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);

            foreach (var id in ids)
            {
                var res = await http.GetAsync($"auth/v1/admin/users/{id}");
                if (!res.IsSuccessStatusCode) continue;

                var json = await res.Content.ReadAsStringAsync();
                var user = System.Text.Json.JsonSerializer.Deserialize<AuthUser>(
                    json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (user != null)
                {
                    user.Username = usernames.GetValueOrDefault(id);
                    users.Add(user);
                }
            }

            await MarkProfilesAsync(users);
            await MarkSubscriptionsAsync(users);

            profile = NormalizeProfileFilter(profile);
            if (profile != "all")
                users = users.Where(u => u.HasProfile == (profile == "with")).ToList();

            return Json(users);
        }
        public async Task<List<Guid>> SearchUserIds(string search)
        {
            var response = await _supabase
                .Rpc<List<Dictionary<string, Guid>>>(
                    "search_auth_users",
                    new { search_text = search }
                );

            return response.Select(x => x["user_id"]).ToList();
        }


        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] BanUserRequest request)
        {
            if (request == null || !Guid.TryParse(request.UserId, out var userId))
                return BadRequest();

            try
            {
                // Service-role client so RLS can't block the cleanup
                var supabase = new Supabase.Client(
                    ApiClientFactory.BaseUrl,
                    ApiClientFactory.ServiceRole
                );
                await supabase.InitializeAsync();

                // Support tickets and their messages
                var tickets = await supabase
                    .From<YourApp.Models.Support.Ticket>()
                    .Where(t => t.UserId == userId)
                    .Get();

                var ticketIds = tickets.Models.Select(t => t.Id).ToList();
                if (ticketIds.Any())
                {
                    await supabase
                        .From<YourApp.Models.Support.TicketMessage>()
                        .Filter("ticket_id", Supabase.Postgrest.Constants.Operator.In, ticketIds)
                        .Delete();
                }

                await supabase
                    .From<YourApp.Models.Support.TicketMessage>()
                    .Where(m => m.SenderId == userId)
                    .Delete();

                await supabase
                    .From<YourApp.Models.Support.Ticket>()
                    .Where(t => t.UserId == userId)
                    .Delete();

                // Suggestions submitted by the user
                await supabase
                    .From<RevUpIQ.Admin.Models.MiniGames.TriviaSubmission>()
                    .Where(s => s.Creator_Id == userId)
                    .Delete();

                await supabase
                    .From<RevUpIQ.Admin.Models.MiniGames.PhotoGuessSubmission>()
                    .Where(s => s.Creator_Id == userId)
                    .Delete();

                await supabase
                    .From<RevUpIQ.Admin.Models.MiniGames.RaceWordSubmission>()
                    .Where(s => s.Creator_Id == userId)
                    .Delete();

                // Leagues owned by the user
                await supabase
                    .From<RevUpIQ.Admin.Models.Leagues.LeagueDto>()
                    .Where(l => l.OwnerId == userId)
                    .Delete();

                // Membership in other players' leagues (joined_users has no foreign key)
                var userIdText = userId.ToString();
                var memberOf = await supabase
                    .From<RevUpIQ.Admin.Models.Leagues.LeagueDto>()
                    .Filter("joined_users", Supabase.Postgrest.Constants.Operator.Contains, new List<string> { userIdText })
                    .Get();

                foreach (var league in memberOf.Models)
                {
                    var leagueId = league.Id;
                    var remaining = league.JoinedUsers.Where(u => u != userIdText).ToArray();
                    await supabase
                        .From<RevUpIQ.Admin.Models.Leagues.LeagueDto>()
                        .Where(l => l.Id == leagueId)
                        .Set(l => l.JoinedUsers, remaining)
                        .Update();
                }

                // League invites sent or received (no foreign key to the user either)
                await supabase
                    .From<RevUpIQ.Admin.Models.Leagues.LeagueInviteDto>()
                    .Where(i => i.InviterId == userId)
                    .Delete();

                await supabase
                    .From<RevUpIQ.Admin.Models.Leagues.LeagueInviteDto>()
                    .Where(i => i.InviteeId == userId)
                    .Delete();

                // Profile and cover photos: the "<id>/" folder plus the older
                // profile_<id>.jpg / cover_<id>.jpg names the app also uploads
                var userImages = supabase.Storage.From("user-images");
                var imageFiles = await userImages.List(userIdText) ?? new();
                var imagePaths = imageFiles
                    .Where(f => !string.IsNullOrEmpty(f.Name))
                    .Select(f => $"{userIdText}/{f.Name}")
                    .ToList();
                imagePaths.Add($"profile_{userIdText}.jpg");
                imagePaths.Add($"cover_{userIdText}.jpg");
                await userImages.Remove(imagePaths);

                // Photos from the user's Photo Guess suggestions ("<id>/" folder).
                // Daily puzzles keep their own copies in the "photoguess" bucket.
                var suggestionImages = supabase.Storage.From("photo_guess_images");
                var suggestionFiles = await suggestionImages.List(userIdText) ?? new();
                var suggestionPaths = suggestionFiles
                    .Where(f => !string.IsNullOrEmpty(f.Name))
                    .Select(f => $"{userIdText}/{f.Name}")
                    .ToList();
                if (suggestionPaths.Count > 0)
                    await suggestionImages.Remove(suggestionPaths);

                // Game data, divisions, profile
                await supabase
                    .From<UserGamePlay>()
                    .Where(g => g.User_Id == userId)
                    .Delete();

                await supabase
                    .From<UserDivision>()
                    .Where(d => d.User_Id == userId)
                    .Delete();

                await supabase
                    .From<Profile>()
                    .Where(p => p.Id == userId)
                    .Delete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteUser data cleanup failed: {ex.Message}");
                return StatusCode(500, $"Failed to delete user data: {ex.Message}");
            }

            // Finally remove the auth account
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);

            var url = $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/auth/v1/admin/users/{userId}";
            var res = await http.DeleteAsync(url);
            var body = await res.Content.ReadAsStringAsync();

            Console.WriteLine($"DeleteUser response: {(int)res.StatusCode} {res.ReasonPhrase}");

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, body);

            return Ok();
        }

        // Clears the "tour seen" flag so the player gets the first-time lobby tour again
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetTour(Guid userId)
        {
            if (userId == Guid.Empty)
                return BadRequest();

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);

            var url = $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/rest/v1/profiles?id=eq.{userId}";
            using var content = new StringContent(
                "{\"tour_completed_at\":null}", System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            msg.Headers.Add("Prefer", "return=representation");

            var res = await http.SendAsync(msg);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"ResetTour failed: {(int)res.StatusCode} {body}");
                return StatusCode(500);
            }

            // "[]" means no profile row matched this user
            if (body.Trim() == "[]")
                return NotFound();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFreeAccount(Guid userId, bool isFree)
        {
            var supabase = new Supabase.Client(
                ApiClientFactory.BaseUrl,
                ApiClientFactory.ServiceRole
            );

            await supabase.InitializeAsync();

            await supabase
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Free_Account, isFree)
                .Update();

            return Ok();
        }


    }
}
