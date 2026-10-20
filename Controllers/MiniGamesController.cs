using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.MiniGames;
using RevUpIQ.Admin.Infrastructure;
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
                DisplayTime.Today.ToString("yyyy-MM-dd"));

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

        #region Trivia Import
        // Bulk adds trivia questions for one division from a CSV file or a Google Sheet:
        // Preview checks every row and shows what will happen, Import adds the ready ones.
        // The app shows one question per division per day, so a day that already has a
        // question is skipped rather than given a second one.

        private const int MaxImportBytes = 2_000_000;
        private const int MaxImportRows = 1000;

        // Header names accepted for each field, compared without case, spaces or punctuation
        private static readonly Dictionary<string, string> TriviaImportColumns = new()
        {
            ["date"] = "date", ["gamedate"] = "date", ["day"] = "date",
            ["question"] = "question",
            ["correctanswer"] = "real", ["correct"] = "real", ["realanswer"] = "real",
            ["rightanswer"] = "real", ["answer"] = "real",
            ["wronganswer1"] = "wrong1", ["wrong1"] = "wrong1", ["answer1"] = "wrong1", ["answera"] = "wrong1",
            ["wronganswer2"] = "wrong2", ["wrong2"] = "wrong2", ["answer2"] = "wrong2", ["answerb"] = "wrong2",
            ["wronganswer3"] = "wrong3", ["wrong3"] = "wrong3", ["answer3"] = "wrong3", ["answerc"] = "wrong3",
        };

        // ISO dates, US month/day dates (Google Sheets and Excel in US locales) and written-out months
        private static readonly string[] ImportDateFormats =
        {
            "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d",
            "M/d/yyyy", "MM/dd/yyyy", "M/d/yy",
            "MMMM d, yyyy", "MMM d, yyyy", "d MMMM yyyy", "d MMM yyyy", "d-MMM-yyyy", "d-MMM-yy",
        };

        [HttpGet]
        public async Task<IActionResult> ImportTrivia(int divisionId)
        {
            var vm = await NewTriviaImportAsync(divisionId);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImportBytes + 100_000)]
        public async Task<IActionResult> PreviewTriviaImport(int divisionId, IFormFile? file, string? sheetUrl)
        {
            var vm = await NewTriviaImportAsync(divisionId);
            if (vm == null) return NotFound();

            string? text = null;
            if (file != null && file.Length > 0)
            {
                vm.Source = file.FileName;
                if (file.Length > MaxImportBytes)
                    vm.Error = "That file is too big. Split it into files under 2 MB.";
                else
                {
                    using var reader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8, true);
                    text = await reader.ReadToEndAsync();
                }
            }
            else if (!string.IsNullOrWhiteSpace(sheetUrl))
            {
                vm.Source = "Google Sheet";
                (text, vm.Error) = await DownloadGoogleSheetAsync(sheetUrl.Trim());
            }
            else
                vm.Error = "Choose a CSV file or paste a Google Sheet link.";

            if (text != null)
            {
                (vm.Rows, vm.Error) = ReadTriviaRows(CsvTable.Parse(text));
                if (vm.Error == null)
                    await CheckTriviaRowsAsync(divisionId, vm.Rows);
            }

            return View("ImportTrivia", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportTriviaRows(int divisionId, string? rowsJson)
        {
            var vm = await NewTriviaImportAsync(divisionId);
            if (vm == null) return NotFound();

            // The preview's ready rows, checked again: the form can be edited, and another
            // admin may have scheduled a question on one of these days since the preview
            try
            {
                vm.Rows = System.Text.Json.JsonSerializer.Deserialize<List<TriviaImportRow>>(rowsJson ?? "") ?? new();
            }
            catch (System.Text.Json.JsonException)
            {
                vm.Error = "The preview couldn't be read. Preview the file again.";
                return View("ImportTrivia", vm);
            }

            await CheckTriviaRowsAsync(divisionId, vm.Rows);
            var ready = vm.Ready;
            if (ready.Count > 0)
            {
                await _supabase.From<TriviaDailyDto>().Insert(ready.Select(r => new TriviaDailyDto
                {
                    DivisionId = divisionId,
                    GameDate = DateTime.ParseExact(r.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    Question = r.Question,
                    RealAnswer = r.RealAnswer,
                    Answer1 = r.Answer1,
                    Answer2 = r.Answer2,
                    Answer3 = r.Answer3,
                }).ToList());

                foreach (var r in ready)
                    r.Status = "Imported";
            }

            vm.Imported = ready.Count;
            return View("ImportTrivia", vm);
        }

        [HttpGet]
        public IActionResult TriviaImportTemplate()
        {
            var day = DisplayTime.Today.AddDays(1);
            var csv =
                "date,question,correct_answer,wrong_answer_1,wrong_answer_2,wrong_answer_3\r\n" +
                $"{day:yyyy-MM-dd},Type the question here,The correct answer,A wrong answer,Another wrong answer,A third wrong answer\r\n" +
                $"{day.AddDays(1):yyyy-MM-dd},\"Questions with commas, like this one, go in quotes\",Correct,Wrong,Wrong too,Also wrong\r\n";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "trivia-import-template.csv");
        }

        private async Task<TriviaImportViewModel?> NewTriviaImportAsync(int divisionId)
        {
            var division = (await _supabase.From<RevUpIQ.Admin.Models.Division.DivisionDto>()
                .Where(d => d.Id == divisionId)
                .Get()).Models.FirstOrDefault();

            return division == null
                ? null
                : new TriviaImportViewModel { DivisionId = divisionId, DivisionName = division.Name };
        }

        // Maps the header to fields and returns one TriviaImportRow per non-blank row
        private static (List<TriviaImportRow> Rows, string? Error) ReadTriviaRows(List<string[]> table)
        {
            var rows = new List<TriviaImportRow>();
            if (table.Count == 0)
                return (rows, "The file is empty.");

            var columns = new Dictionary<string, int>();
            for (var i = 0; i < table[0].Length; i++)
            {
                var key = new string(table[0][i].Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (TriviaImportColumns.TryGetValue(key, out var field))
                    columns.TryAdd(field, i);
            }

            var missing = new[] { "date", "question", "real", "wrong1", "wrong2", "wrong3" }
                .Where(f => !columns.ContainsKey(f))
                .Select(f => f switch
                {
                    "real" => "correct_answer",
                    "wrong1" => "wrong_answer_1",
                    "wrong2" => "wrong_answer_2",
                    "wrong3" => "wrong_answer_3",
                    _ => f
                })
                .ToList();
            if (missing.Count > 0)
                return (rows, $"The first row must name the columns. Missing: {string.Join(", ", missing)}. " +
                              "Download the template to see the layout.");

            string Cell(string[] row, string field) =>
                columns[field] < row.Length ? row[columns[field]].Trim() : "";

            for (var i = 1; i < table.Count; i++)
            {
                if (table[i].All(string.IsNullOrWhiteSpace))
                    continue;

                rows.Add(new TriviaImportRow
                {
                    Row = i + 1,
                    Date = Cell(table[i], "date"),
                    Question = Cell(table[i], "question"),
                    RealAnswer = Cell(table[i], "real"),
                    Answer1 = Cell(table[i], "wrong1"),
                    Answer2 = Cell(table[i], "wrong2"),
                    Answer3 = Cell(table[i], "wrong3"),
                });
            }

            if (rows.Count == 0)
                return (rows, "The file has a header but no questions.");
            if (rows.Count > MaxImportRows)
                return (new List<TriviaImportRow>(), $"The file has {rows.Count} questions; import at most {MaxImportRows} at a time.");

            return (rows, null);
        }

        // Sets each row's Status and Note: Error with the reason, Skipped when the day already
        // has a question for this division, otherwise Ready
        private async Task CheckTriviaRowsAsync(int divisionId, List<TriviaImportRow> rows)
        {
            var today = DisplayTime.Today;
            long division = divisionId;
            var taken = (await _supabase.From<TriviaDailyDto>()
                    .Select("id,game_date")
                    .Where(t => t.DivisionId == division)
                    .Filter("game_date", Operator.GreaterThanOrEqual, today.ToString("yyyy-MM-dd"))
                    .Get())
                .Models.Select(t => t.GameDate.Date)
                .ToHashSet();

            var dates = new HashSet<DateTime>();
            foreach (var r in rows)
            {
                r.Status = "Error";
                r.Question = r.Question.Trim();
                r.RealAnswer = r.RealAnswer.Trim();
                r.Answer1 = r.Answer1.Trim();
                r.Answer2 = r.Answer2.Trim();
                r.Answer3 = r.Answer3.Trim();

                if (!DateTime.TryParseExact(r.Date.Trim(), ImportDateFormats, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date))
                {
                    r.Note = "The date must look like 2026-11-05 or 11/5/2026.";
                    continue;
                }

                r.Date = date.ToString("yyyy-MM-dd");
                var answers = new[] { r.RealAnswer, r.Answer1, r.Answer2, r.Answer3 };

                if (date < today)
                    r.Note = "The date is in the past.";
                else if (r.Question.Length == 0)
                    r.Note = "The question is empty.";
                else if (answers.Any(a => a.Length == 0))
                    r.Note = "Every question needs a correct answer and three wrong answers.";
                else if (answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() < 4)
                    r.Note = "Two of the answers are the same.";
                else if (!dates.Add(date))
                    r.Note = "Another row in this file uses the same date.";
                else if (taken.Contains(date))
                {
                    r.Status = "Skipped";
                    r.Note = "This day already has a question.";
                }
                else
                {
                    r.Status = "Ready";
                    r.Note = null;
                }
            }
        }

        // Only Google Sheets links are fetched, and the address is rebuilt from the sheet's
        // id, so a pasted link can't point the server anywhere else
        private static async Task<(string? Text, string? Error)> DownloadGoogleSheetAsync(string link)
        {
            var match = System.Text.RegularExpressions.Regex.Match(link,
                @"^https://docs\.google\.com/spreadsheets/d/(e/)?([A-Za-z0-9_-]+)");
            if (!match.Success)
                return (null, "Paste a Google Sheets link, starting with https://docs.google.com/spreadsheets/d/");

            var gid = System.Text.RegularExpressions.Regex.Match(link, @"[#&?]gid=(\d+)");
            var url = match.Groups[1].Success
                ? $"https://docs.google.com/spreadsheets/d/e/{match.Groups[2].Value}/pub?output=csv"
                : $"https://docs.google.com/spreadsheets/d/{match.Groups[2].Value}/export?format=csv";
            if (gid.Success)
                url += $"&gid={gid.Groups[1].Value}";

            const string notShared =
                "Google didn't share that sheet. In Google Sheets choose Share → General access → " +
                "\"Anyone with the link\" (Viewer), or download it as a CSV file and upload that instead.";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var type = res.Content.Headers.ContentType?.MediaType ?? "";
                if (!res.IsSuccessStatusCode || type.Contains("html"))
                    return (null, notShared);
                if (res.Content.Headers.ContentLength > MaxImportBytes)
                    return (null, "That sheet is too big. Split it into sheets under 2 MB.");

                var text = await res.Content.ReadAsStringAsync();
                return text.Length > MaxImportBytes
                    ? (null, "That sheet is too big. Split it into sheets under 2 MB.")
                    : (text, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return (null, "Google Sheets couldn't be reached. Try again, or upload the sheet as a CSV file.");
            }
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
        private const int MaxCategoryLength = 50;

        // Trims whitespace; empty becomes null so the word has no category
        private static bool TryNormalizeCategory(string? raw, out string? category)
        {
            var trimmed = raw?.Trim();
            category = string.IsNullOrEmpty(trimmed) ? null : trimmed;
            return category == null || category.Length <= MaxCategoryLength;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRaceWord(int DivisionId, WordleWord model, string Date)
        {
            var gameDate = DateTime.Parse(Date).Date;

            if (!TryNormalizeCategory(model.Category, out var category))
                return BadRequest($"Category must be {MaxCategoryLength} characters or fewer.");

            var raceWord = new RaceWordDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                TargetWord = model.TargetWord,
                Category = category,
            };

            await _supabase.From<RaceWordDailyDto>().Insert(raceWord);

            return Json(await EnsureWordIsPlayableAsync(model.TargetWord));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRaceWord(long id, WordleWord model)
        {
            if (!TryNormalizeCategory(model.Category, out var category))
                return BadRequest($"Category must be {MaxCategoryLength} characters or fewer.");

            var existing = await _supabase
                .From<RaceWordDailyDto>()
                .Where(x => x.Id == id)
                .Single();

            if (existing == null)
                return NotFound();

            existing.TargetWord = model.TargetWord;
            existing.Category = category;
            await _supabase.From<RaceWordDailyDto>().Update(existing);

            return Json(await EnsureWordIsPlayableAsync(model.TargetWord));
        }

        // Live check used by the create/edit forms while the admin types the word
        [HttpGet]
        public async Task<IActionResult> CheckRaceWord(string word)
        {
            var w = WordDictionaryClient.Normalize(word);
            if (w == null)
                return Json(new { status = "invalid" });

            var status = await WordDictionaryClient.CheckAsync(w);
            return Json(new { status = status.ToString() });
        }

        // Players must pass the app's dictionary check to submit a guess, including
        // the correct answer. If the word is in neither dictionary it is added to the
        // custom dictionary so the daily game can always be won.
        private async Task<object> EnsureWordIsPlayableAsync(string? targetWord)
        {
            var w = WordDictionaryClient.Normalize(targetWord);
            if (w == null)
                return new { addedToDictionary = false, status = "invalid" };

            var status = await WordDictionaryClient.CheckAsync(w);

            if (status is WordDictionaryStatus.InDictionary or WordDictionaryStatus.InCustom)
                return new { addedToDictionary = false, status = status.ToString() };

            var added = await WordDictionaryClient.AddToCustomAsync(w);
            return new { addedToDictionary = added, status = status.ToString(), word = w };
        }

        #endregion
    }
}

