using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.MiniGames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RevUpIQ.Admin.Controllers
{
    public class UsersSuggestionController : Controller
    {
        private readonly Supabase.Client _supabase;

        public UsersSuggestionController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public IActionResult Index()
        {
            return View("~/Views/Exrta/UsersSuggestion.cshtml");
        }

        public async Task<IActionResult> SuggestionList(string mode)
        {
            var items = new List<CombinedSuggestion>();
            var normalizedMode = mode?.Trim();

            if (string.IsNullOrEmpty(normalizedMode) ||
                normalizedMode.Equals("Combined", StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(await LoadRaceWord());
                items.AddRange(await LoadTrivia());
                items.AddRange(await LoadPhotoGuess());
            }
            else if (normalizedMode.Equals(GameModeType.RaceWord.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(await LoadRaceWord());
            }
            else if (normalizedMode.Equals(GameModeType.Trivia.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(await LoadTrivia());
            }
            else if (normalizedMode.Equals(GameModeType.PhotoGuess.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(await LoadPhotoGuess());
            }

            var usersController = new UsersController(_supabase);

            foreach (var item in items)
            {
                var uid =
                    item.RaceWord?.Creator_Id ??
                    item.Trivia?.Creator_Id ??
                    item.PhotoGuess?.Creator_Id ?? Guid.Empty;

                if (uid != Guid.Empty)
                    item.UserProfile = await usersController.LoadUserProfileAsync(uid.ToString());
            }

            items = items
                .OrderByDescending(i =>
                    i.RaceWord?.Created_At ??
                    i.Trivia?.Created_At ??
                    i.PhotoGuess?.Created_At)
                .ToList();

            ViewBag.Mode = string.IsNullOrEmpty(normalizedMode) ? "Combined" : normalizedMode;

            return PartialView("~/Views/Exrta/_SuggestionList.cshtml", items);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteSuggestion(long id, string mode)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(mode))
                return BadRequest();

            if (mode == GameModeType.RaceWord.ToString())
                await _supabase.From<RaceWordSubmission>().Where(x => x.Id == id).Delete();

            else if (mode == GameModeType.Trivia.ToString())
                await _supabase.From<TriviaSubmission>().Where(x => x.Id == id).Delete();

            else if (mode == GameModeType.PhotoGuess.ToString())
                await _supabase.From<PhotoGuessSubmission>().Where(x => x.Id == id).Delete();

            else
                return BadRequest();

            return Ok();
        }


        [HttpGet]
        public async Task<IActionResult> SuggestionDetails(long id, string mode)
        {
            var vm = new CombinedSuggestion();
            var usersController = new UsersController(_supabase);

            if (mode == GameModeType.RaceWord.ToString())
            {
                var result = await _supabase
                    .From<RaceWordSubmission>()
                    .Where(x => x.Id == id)
                    .Get();

                var item = result.Models.FirstOrDefault();
                if (item == null) return NotFound();

                vm.RaceWord = item;
                vm.UserProfile = await usersController.LoadUserProfileAsync(item.Creator_Id.ToString());
            }
            else if (mode == GameModeType.Trivia.ToString())
            {
                var result = await _supabase
                    .From<TriviaSubmission>()
                    .Where(x => x.Id == id)
                    .Get();

                var item = result.Models.FirstOrDefault();
                if (item == null) return NotFound();

                vm.Trivia = item;
                vm.UserProfile = await usersController.LoadUserProfileAsync(item.Creator_Id.ToString());
            }
            else if (mode == GameModeType.PhotoGuess.ToString())
            {
                var result = await _supabase
                    .From<PhotoGuessSubmission>()
                    .Where(x => x.Id == id)
                    .Get();

                var item = result.Models.FirstOrDefault();
                if (item == null) return NotFound();

                vm.PhotoGuess = item;
                vm.UserProfile = await usersController.LoadUserProfileAsync(item.Creator_Id.ToString());
            }
            else
            {
                return BadRequest();
            }
            return View("~/Views/Exrta/SuggestionDetails.cshtml", vm);
        }


        private async Task<IEnumerable<CombinedSuggestion>> LoadRaceWord()
        {
            var response = await _supabase.From<RaceWordSubmission>().Get();
            return response.Models.Select(x => new CombinedSuggestion { RaceWord = x });
        }

        private async Task<IEnumerable<CombinedSuggestion>> LoadTrivia()
        {
            var response = await _supabase.From<TriviaSubmission>().Get();
            return response.Models.Select(x => new CombinedSuggestion { Trivia = x });
        }

        private async Task<IEnumerable<CombinedSuggestion>> LoadPhotoGuess()
        {
            var response = await _supabase.From<PhotoGuessSubmission>().Get();
            return response.Models.Select(x => new CombinedSuggestion { PhotoGuess = x });
        }
    }
}
