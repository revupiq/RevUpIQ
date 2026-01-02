using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.MiniGames;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    public class MiniGamesController : Controller
    {
        private readonly Client _supabase;
        private const string BucketName = "photoguess";

        public MiniGamesController(Client supabase)
        {
            _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        }

        public IActionResult DailyGameFilter(int id)
        {
            var model = new DailyGameFilter(
                id,
                GameModeType.RaceWord,
                DateTime.Today.ToString("yyyy-MM-dd"));

            Console.WriteLine($"[DailyGameFilter] Mode={model.Mode}, Date={model.Date}, DivisionId={model.Id}");
            return PartialView("_DailyGameFilter", model);
        }

        public async Task<IActionResult> Calendar(int divisionId, GameModeType mode, int year, int month, string? date)
        {
            var monthBase = new DateTime(year, month, 1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var daysWithGames = await GetDaysWithGames(divisionId, mode, monthBase);

            var dayFlags = new Dictionary<int, bool>(daysInMonth);
            for (int d = 1; d <= daysInMonth; d++)
                dayFlags[d] = daysWithGames.Contains(d);

            ViewData["DayFlags"] = dayFlags;
            ViewData["CalendarMonthBase"] = monthBase;

            var model = new DailyGameFilter(divisionId, mode, date ?? "");
            return PartialView("_Calendar", model);
        }

        private async Task<HashSet<int>> GetDaysWithGames(int divisionId, GameModeType mode, DateTime monthBase)
        {
            var end = monthBase.AddMonths(1);

            string startDate = monthBase.ToString("yyyy-MM-dd");
            string endDate = end.ToString("yyyy-MM-dd");

            return mode switch
            {
                GameModeType.Trivia => (await _supabase
                        .From<TriviaDailyDto>()
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, startDate)
                        .Filter("game_date", Operator.LessThan, endDate)
                        .Get())
                    .Models.Select(x => x.GameDate.Day).ToHashSet(),

                GameModeType.PhotoGuess => (await _supabase
                        .From<PhotoGuessDailyDto>()
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, startDate)
                        .Filter("game_date", Operator.LessThan, endDate)
                        .Get())
                    .Models.Select(x => x.GameDate.Day).ToHashSet(),

                GameModeType.RaceWord => (await _supabase
                        .From<RaceWordDailyDto>()
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, startDate)
                        .Filter("game_date", Operator.LessThan, endDate)
                        .Get())
                    .Models.Select(x => x.GameDate.Day).ToHashSet(),

                _ => new HashSet<int>()
            };
        }




        [HttpPost]
        public async Task<IActionResult> SetGameMode(int DivisionId, string Mode, string Date)
        {
            Console.WriteLine($"MODE SELECTED  => {Mode}");
            Console.WriteLine($"DATE SELECTED => {Date}");
            Console.WriteLine($"DIVISION ID => {DivisionId}");

            var gameMode = Enum.Parse<GameModeType>(Mode);
            var gameDate = DateTime.Parse(Date).Date;

            var gameData = await LoadGameData(DivisionId, gameMode, gameDate);

            var model = new DailyGameFilter(DivisionId, gameMode, Date);
            ViewData["GameData"] = gameData;

            return PartialView("_MiniGameViewer", model);

        }

        [HttpPost]
        public async Task<IActionResult> EditMiniGame(int DivisionId, string Mode, string Date)
        {
            var gameMode = Enum.Parse<GameModeType>(Mode);
            var gameDate = DateTime.Parse(Date).Date;

            var gameData = await LoadGameData(DivisionId, gameMode, gameDate);
            ViewData["GameData"] = gameData;
            Console.WriteLine(gameData);

            return Mode switch
            {
                "PhotoGuess" => View("~/Views/MiniGames/PhotoGuess/Edit.cshtml"),
                "Trivia" => View("~/Views/MiniGames/Trivia/Edit.cshtml"),
                "RaceWord" => View("~/Views/MiniGames/Wordle/Edit.cshtml"),
                _ => NotFound()
            };
        }



        [HttpPost]
        public IActionResult CreateMiniGame(int DivisionId, string Mode, string Date)
        {
            ViewData["DivisionId"] = DivisionId;
            ViewData["Date"] = Date;
            return Mode switch
            {
                "PhotoGuess" => View("~/Views/MiniGames/PhotoGuess/Create.cshtml"),
                "Trivia" => View("~/Views/MiniGames/Trivia/Create.cshtml"),
                "RaceWord" => View("~/Views/MiniGames/Wordle/Create.cshtml"),
                _ => NotFound()
            };
        }

        private async Task<object?> LoadGameData(int divisionId, GameModeType mode, DateTime date)
        {
            return mode switch
            {
                GameModeType.Trivia => (await _supabase
                    .From<TriviaDailyDto>()
                    .Where(t => t.DivisionId == divisionId && t.GameDate == date)
                    .Get()).Models.FirstOrDefault(),

                GameModeType.PhotoGuess => (await _supabase
                    .From<PhotoGuessDailyDto>()
                    .Where(t => t.DivisionId == divisionId && t.GameDate == date)
                    .Get()).Models.FirstOrDefault(),

                GameModeType.RaceWord => (await _supabase
                    .From<RaceWordDailyDto>()
                    .Where(t => t.DivisionId == divisionId && t.GameDate == date)
                    .Get()).Models.FirstOrDefault(),

                _ => null
            };
        }


        public async Task<IActionResult> MiniGameSetting(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode == "undefined")
                mode = "raceWord";
            mode = mode switch
            {
                "RaceWord" => "raceWord",
                "PhotoGuess" => "photoGuess",
                "Trivia" => "trivia",
                _ => mode
            };
            var setting = (await _supabase
                .From<MiniGameSetting>()
                .Where(x => x.GameMode == mode)
                .Single())
                ?? new MiniGameSetting
                {
                    GameMode = mode,
                    TimerSeconds = 22,
                    MaxScore = 22
                };

            return PartialView("_MiniGamesSetting", setting);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMiniGameSetting(string mode, int timer, int maxScore)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode == "undefined")
                mode = "raceWord";

            mode = mode switch
            {
                "RaceWord" => "raceWord",
                "PhotoGuess" => "photoGuess",
                "Trivia" => "trivia",
                _ => mode
            };

            maxScore = timer;

            Console.WriteLine($"[UpdateMiniGameSetting] normalized mode={mode}, timer={timer}, maxScore={maxScore}");

            var existing = (await _supabase
                .From<MiniGameSetting>()
                .Where(x => x.GameMode == mode)
                .Single());

            if (existing != null)
            {
                existing.TimerSeconds = timer;
                existing.MaxScore = maxScore;

                await _supabase.From<MiniGameSetting>().Update(existing);

                return NoContent();
            }

            var setting = new MiniGameSetting
            {
                GameMode = mode,
                TimerSeconds = timer,
                MaxScore = maxScore
            };

            await _supabase.From<MiniGameSetting>().Insert(setting);

            return NoContent();
        }




        #region Trivia Actions
        [HttpPost]
        public async Task<IActionResult> UpdateTrivia(int id, TriviaQuestion model)
        {
            await _supabase
                .From<TriviaDailyDto>()
                .Where(t => t.Id == id)
                .Set(t => t.Question, model.Question)
                .Set(t => t.RealAnswer, model.RealAnswer)
                .Set(t => t.Answer1, model.Answer1)
                .Set(t => t.Answer2, model.Answer2)
                .Set(t => t.Answer3, model.Answer3)
                .Update();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTrivia(int DivisionId, TriviaQuestion model, string Date)
        {
            var gameDate = DateTime.Parse(Date).Date;

            var trivia = new TriviaDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                Question = model.Question,
                RealAnswer = model.RealAnswer,
                Answer1 = model.Answer1,
                Answer2 = model.Answer2,
                Answer3 = model.Answer3,
            };
            await _supabase.From<TriviaDailyDto>().Insert(trivia);
            return Ok();
        }
        #endregion

        #region Photo Guess Actions
        [HttpPost]
        public async Task<IActionResult> CreatePhotoGuess(int DivisionId, PhotoGuessItem model, string Date, IFormFile? ImageFile)
        {
            if (ImageFile == null || ImageFile.Length == 0)
                return BadRequest("Image is required.");

            var gameDate = DateTime.Parse(Date).Date;

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
            string imageUrl;

            using (var ms = new MemoryStream())
            {
                await ImageFile.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var bucket = _supabase.Storage.From(BucketName);
                await bucket.Upload(bytes, fileName);
                imageUrl = bucket.GetPublicUrl(fileName);
            }

            var photoGuess = new PhotoGuessDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                Question = model.Question,
                RealAnswer = model.RealAnswer,
                Answer1 = model.Answer1,
                Answer2 = model.Answer2,
                Answer3 = model.Answer3,
                ImageUrl = imageUrl
            };

            await _supabase.From<PhotoGuessDailyDto>().Insert(photoGuess);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePhotoGuess(long id, PhotoGuessItem model, IFormFile? ImageFile)
        {
            var existing = await _supabase
                .From<PhotoGuessDailyDto>()
                .Where(x => x.Id == id)
                .Single();

            if (existing == null)
                return NotFound();

            existing.Question = model.Question;
            existing.RealAnswer = model.RealAnswer;
            existing.Answer1 = model.Answer1;
            existing.Answer2 = model.Answer2;
            existing.Answer3 = model.Answer3;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";

                using (var ms = new MemoryStream())
                {
                    await ImageFile.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    var bucket = _supabase.Storage.From(BucketName);
                    await bucket.Upload(bytes, fileName);

                    existing.ImageUrl = bucket.GetPublicUrl(fileName);
                }
            }

            await _supabase.From<PhotoGuessDailyDto>().Update(existing);

            return Ok();
        }
        #endregion

        #region Race Word Actions
        [HttpPost]
        public async Task<IActionResult> CreateRaceWord(int DivisionId, WordleWord model, string Date)
        {
            var gameDate = DateTime.Parse(Date).Date;

            var raceWord = new RaceWordDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                TargetWord = model.TargetWord,
            };

            await _supabase.From<RaceWordDailyDto>().Insert(raceWord);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRaceWord(long id, WordleWord model)
        {
            var existing = await _supabase
                .From<RaceWordDailyDto>()
                .Where(x => x.Id == id)
                .Single();

            if (existing == null)
                return NotFound();

            existing.TargetWord = model.TargetWord;
            await _supabase.From<RaceWordDailyDto>().Update(existing);

            return Ok();
        }

        #endregion
    }
}

