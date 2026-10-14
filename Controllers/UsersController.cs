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

            var profileIds = new HashSet<Guid>();

            foreach (var group in guids.Chunk(100))
            {
                var result = await _supabase
                    .From<Profile>()
                    .Select("id")
                    .Filter("id", Supabase.Postgrest.Constants.Operator.In, group.ToList())
                    .Get();

                foreach (var p in result.Models)
                    profileIds.Add(p.Id);
            }

            foreach (var u in users)
                u.HasProfile = Guid.TryParse(u.Id, out var id) && profileIds.Contains(id);
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(string id)
        {
            var vm = await LoadUserProfileAsync(id);
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

            return Ok();
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
                    users.Add(user);
            }

            await MarkProfilesAsync(users);

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
