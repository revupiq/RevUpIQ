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

        // Optional fact the app shows under the correct answer on the win and lose screens
        public const int MaxFunFactLength = 180;

        public MiniGamesController(Client supabase)
        {
            _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        }

        // Trims whitespace; empty becomes null so the question has no fun fact. Line breaks
        // count as one character, like the form's counter.
        private static bool TryNormalizeFunFact(string? raw, out string? funFact)
        {
            var trimmed = raw?.Replace("\r\n", "\n").Trim();
            funFact = string.IsNullOrEmpty(trimmed) ? null : trimmed;
            return funFact == null || funFact.Length <= MaxFunFactLength;
        }

        private static string FunFactTooLong => $"The fun fact must be {MaxFunFactLength} characters or fewer.";

        // Photo Guess: who took the photo, shown in small type under it in the app
        public const int MaxPhotoCreditLength = 100;

        // One line, trimmed; empty becomes null so no credit line shows
        private static bool TryNormalizePhotoCredit(string? raw, out string? credit)
        {
            var oneLine = string.Join(" ", (raw ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            credit = oneLine.Length == 0 ? null : oneLine;
            return credit == null || credit.Length <= MaxPhotoCreditLength;
        }

        private static string PhotoCreditTooLong => $"The photo credit must be {MaxPhotoCreditLength} characters or fewer.";

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

            // Photo Guess days whose question has no image yet: the app skips them until one is added
            if (mode == GameModeType.PhotoGuess)
                ViewData["NoImageDays"] = (await _supabase
                        .From<PhotoGuessDailyDto>()
                        .Select("id,game_date,image_url")
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, monthBase.ToString("yyyy-MM-dd"))
                        .Filter("game_date", Operator.LessThan, monthBase.AddMonths(1).ToString("yyyy-MM-dd"))
                        .Get())
                    .Models.Where(x => string.IsNullOrWhiteSpace(x.ImageUrl))
                    .Select(x => x.GameDate.Day)
                    .ToHashSet();

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
            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);

            await _supabase
                .From<TriviaDailyDto>()
                .Where(t => t.Id == id)
                .Set(t => t.Question, model.Question)
                .Set(t => t.RealAnswer, model.RealAnswer)
                .Set(t => t.Answer1, model.Answer1)
                .Set(t => t.Answer2, model.Answer2)
                .Set(t => t.Answer3, model.Answer3)
                .Set(t => t.FunFact!, funFact)
                .Update();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTrivia(int DivisionId, TriviaQuestion model, string Date)
        {
            var gameDate = DateTime.Parse(Date).Date;

            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);

            var trivia = new TriviaDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                Question = model.Question,
                RealAnswer = model.RealAnswer,
                Answer1 = model.Answer1,
                Answer2 = model.Answer2,
                Answer3 = model.Answer3,
                FunFact = funFact,
            };
            await _supabase.From<TriviaDailyDto>().Insert(trivia);
            return Ok();
        }
        #endregion

        #region Daily Game Imports
        // Bulk adds Trivia, Photo Guess or RaceWord questions for one division from a CSV file
        // or a Google Sheet: Preview checks every row and shows what will happen, Import adds
        // the ready ones. The app shows one question per division per day, so a day that
        // already has a question is skipped rather than given a second one.

        private const int MaxImportBytes = 2_000_000;
        private const int MaxImportRows = 1000;
        private const int MaxImportImageBytes = 10_000_000;

        // Images and dictionary lookups for one file are fetched a few at a time
        private static readonly ParallelOptions ImportLookups = new() { MaxDegreeOfParallelism = 6 };

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
            ["funfact"] = "funfact", ["fact"] = "funfact",
        };

        private static readonly Dictionary<string, string> PhotoGuessImportColumns = new(TriviaImportColumns)
        {
            ["imageurl"] = "image", ["image"] = "image", ["imagelink"] = "image",
            ["photo"] = "image", ["photourl"] = "image", ["photolink"] = "image",
            ["photocredit"] = "credit", ["credit"] = "credit", ["imagecredit"] = "credit", ["photoby"] = "credit",
        };

        private static readonly Dictionary<string, string> RaceWordImportColumns = new()
        {
            ["date"] = "date", ["gamedate"] = "date", ["day"] = "date",
            ["word"] = "word", ["targetword"] = "word", ["raceword"] = "word", ["answer"] = "word",
            ["category"] = "category",
            ["funfact"] = "funfact", ["fact"] = "funfact",
        };

        // Columns the first row must name, with the header the template uses
        private static readonly (string Field, string Header)[] QuestionImportRequired =
        {
            ("date", "date"), ("question", "question"), ("real", "correct_answer"),
            ("wrong1", "wrong_answer_1"), ("wrong2", "wrong_answer_2"), ("wrong3", "wrong_answer_3"),
        };

        private static readonly (string Field, string Header)[] RaceWordImportRequired =
        {
            ("date", "date"), ("word", "word"), ("category", "category"),
        };

        // ISO dates, US month/day dates (Google Sheets and Excel in US locales) and written-out months
        private static readonly string[] ImportDateFormats =
        {
            "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d",
            "M/d/yyyy", "MM/dd/yyyy", "M/d/yy",
            "MMMM d, yyyy", "MMM d, yyyy", "d MMMM yyyy", "d MMM yyyy", "d-MMM-yyyy", "d-MMM-yy",
        };

        private const string NoImageNote = "No image — stays unpublished until you add one.";

        // ---- Trivia ----

        [HttpGet]
        public async Task<IActionResult> ImportTrivia(int divisionId)
        {
            var vm = await NewImportAsync<TriviaImportViewModel>(divisionId);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImportBytes + 100_000)]
        public async Task<IActionResult> PreviewTriviaImport(int divisionId, IFormFile? file, string? sheetUrl)
        {
            var vm = await NewImportAsync<TriviaImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            string? text;
            (text, vm.Source, vm.Error) = await ReadImportSourceAsync(file, sheetUrl);
            if (text != null)
            {
                (vm.Rows, vm.Error) = ReadImportRows(CsvTable.Parse(text), TriviaImportColumns, QuestionImportRequired,
                    "questions", cell => new TriviaImportRow
                    {
                        Question = cell("question"),
                        RealAnswer = cell("real"),
                        Answer1 = cell("wrong1"),
                        Answer2 = cell("wrong2"),
                        Answer3 = cell("wrong3"),
                    });
                if (vm.Error == null)
                    await CheckTriviaRowsAsync(divisionId, vm.Rows);
            }

            return View("ImportTrivia", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportTriviaRows(int divisionId, string? rowsJson)
        {
            var vm = await NewImportAsync<TriviaImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            // The preview's ready rows, checked again: the form can be edited, and another
            // admin may have scheduled a question on one of these days since the preview
            if (!TryReadPreviewRows(rowsJson, vm))
                return View("ImportTrivia", vm);

            await CheckTriviaRowsAsync(divisionId, vm.Rows);
            var ready = vm.Ready;
            if (ready.Count > 0)
            {
                await _supabase.From<TriviaDailyDto>().Insert(ready.Select(r => new TriviaDailyDto
                {
                    DivisionId = divisionId,
                    GameDate = ImportDate(r),
                    Question = r.Question,
                    RealAnswer = r.RealAnswer,
                    Answer1 = r.Answer1,
                    Answer2 = r.Answer2,
                    Answer3 = r.Answer3,
                    FunFact = NullIfEmpty(r.FunFact),
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
                "date,question,correct_answer,wrong_answer_1,wrong_answer_2,wrong_answer_3,fun_fact\r\n" +
                $"{day:yyyy-MM-dd},Type the question here,The correct answer,A wrong answer,Another wrong answer,A third wrong answer,\"Optional: a fun fact shown under the answer, up to 180 characters\"\r\n" +
                $"{day.AddDays(1):yyyy-MM-dd},\"Questions with commas, like this one, go in quotes\",Correct,Wrong,Wrong too,Also wrong,\r\n";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "trivia-import-template.csv");
        }

        // Sets each row's Status and Note: Error with the reason, Skipped when the day already
        // has a question for this division, otherwise Ready
        private async Task CheckTriviaRowsAsync(int divisionId, List<TriviaImportRow> rows)
        {
            var today = DisplayTime.Today;
            var taken = await GetTakenImportDaysAsync(divisionId, GameModeType.Trivia);

            var dates = new HashSet<DateTime>();
            foreach (var r in rows)
            {
                r.Question = r.Question.Trim();
                r.RealAnswer = r.RealAnswer.Trim();
                r.Answer1 = r.Answer1.Trim();
                r.Answer2 = r.Answer2.Trim();
                r.Answer3 = r.Answer3.Trim();

                CheckImportRow(r, QuestionError(r.Question, r.RealAnswer, r.Answer1, r.Answer2, r.Answer3),
                    today, dates, taken);
            }
        }

        // ---- Photo Guess ----

        [HttpGet]
        public async Task<IActionResult> ImportPhotoGuess(int divisionId)
        {
            var vm = await NewImportAsync<PhotoGuessImportViewModel>(divisionId);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImportBytes + 100_000)]
        public async Task<IActionResult> PreviewPhotoGuessImport(int divisionId, IFormFile? file, string? sheetUrl)
        {
            var vm = await NewImportAsync<PhotoGuessImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            string? text;
            (text, vm.Source, vm.Error) = await ReadImportSourceAsync(file, sheetUrl);
            if (text != null)
            {
                (vm.Rows, vm.Error) = ReadImportRows(CsvTable.Parse(text), PhotoGuessImportColumns, QuestionImportRequired,
                    "questions", cell => new PhotoGuessImportRow
                    {
                        Question = cell("question"),
                        RealAnswer = cell("real"),
                        Answer1 = cell("wrong1"),
                        Answer2 = cell("wrong2"),
                        Answer3 = cell("wrong3"),
                        ImageLink = cell("image"),
                        PhotoCredit = cell("credit"),
                    });
                if (vm.Error == null)
                {
                    await CheckPhotoGuessRowsAsync(divisionId, vm.Rows);

                    // Ask each image link for its image now, so a link that won't work shows
                    // here rather than after Import
                    await Parallel.ForEachAsync(vm.Ready.Where(r => r.ImageLink.Length > 0), ImportLookups,
                        async (r, _) =>
                        {
                            var error = await ImportImageClient.CheckAsync(r.ImageLink, MaxImportImageBytes);
                            if (error != null)
                            {
                                r.Status = "Error";
                                r.Note = error;
                            }
                        });
                }
            }

            return View("ImportPhotoGuess", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPhotoGuessRows(int divisionId, string? rowsJson)
        {
            var vm = await NewImportAsync<PhotoGuessImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            if (!TryReadPreviewRows(rowsJson, vm))
                return View("ImportPhotoGuess", vm);

            await CheckPhotoGuessRowsAsync(divisionId, vm.Rows);

            // Each image is downloaded and stored like an uploaded one (CreatePhotoGuess); a row
            // whose image fails becomes an error and the others still import
            await Parallel.ForEachAsync(vm.Ready.Where(r => r.ImageLink.Length > 0), ImportLookups,
                async (r, _) =>
                {
                    var (bytes, extension, error) = await ImportImageClient.DownloadAsync(r.ImageLink, MaxImportImageBytes);
                    if (bytes == null)
                    {
                        r.Status = "Error";
                        r.Note = error;
                        return;
                    }

                    try
                    {
                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var bucket = _supabase.Storage.From(BucketName);
                        await bucket.Upload(bytes, fileName);
                        r.StoredImageUrl = bucket.GetPublicUrl(fileName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ImportPhotoGuessRows] Image upload failed for row {r.Row}: {ex.Message}");
                        r.Status = "Error";
                        r.Note = "The image downloaded but couldn't be saved to storage. Import the file again.";
                    }
                });

            var ready = vm.Ready;
            if (ready.Count > 0)
            {
                await _supabase.From<PhotoGuessDailyDto>().Insert(ready.Select(r => new PhotoGuessDailyDto
                {
                    DivisionId = divisionId,
                    GameDate = ImportDate(r),
                    Question = r.Question,
                    RealAnswer = r.RealAnswer,
                    Answer1 = r.Answer1,
                    Answer2 = r.Answer2,
                    Answer3 = r.Answer3,
                    FunFact = NullIfEmpty(r.FunFact),
                    PhotoCredit = NullIfEmpty(r.PhotoCredit),
                    ImageUrl = r.StoredImageUrl,
                }).ToList());

                foreach (var r in ready)
                    r.Status = "Imported";
            }

            vm.Imported = ready.Count;
            return View("ImportPhotoGuess", vm);
        }

        [HttpGet]
        public IActionResult PhotoGuessImportTemplate()
        {
            var day = DisplayTime.Today.AddDays(1);
            var csv =
                "date,question,correct_answer,wrong_answer_1,wrong_answer_2,wrong_answer_3,fun_fact,image_url,photo_credit\r\n" +
                $"{day:yyyy-MM-dd},Who is this driver?,The correct answer,A wrong answer,Another wrong answer,A third wrong answer,\"Optional: a fun fact shown under the answer, up to 180 characters\",https://drive.google.com/file/d/YOUR_FILE_ID/view?usp=sharing,Optional: Photo: Jane Smith / DirtOnDirt.com\r\n" +
                $"{day.AddDays(1):yyyy-MM-dd},\"No image yet: this one stays unpublished until you add it\",Correct,Wrong,Wrong too,Also wrong,,,\r\n";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "photo-guess-import-template.csv");
        }

        // Like the trivia check, plus the image link: one that isn't a web address is an
        // error, and a Ready row without one is noted as staying unpublished
        private async Task CheckPhotoGuessRowsAsync(int divisionId, List<PhotoGuessImportRow> rows)
        {
            var today = DisplayTime.Today;
            var taken = await GetTakenImportDaysAsync(divisionId, GameModeType.PhotoGuess);

            var dates = new HashSet<DateTime>();
            foreach (var r in rows)
            {
                r.Question = r.Question.Trim();
                r.RealAnswer = r.RealAnswer.Trim();
                r.Answer1 = r.Answer1.Trim();
                r.Answer2 = r.Answer2.Trim();
                r.Answer3 = r.Answer3.Trim();
                r.ImageLink = r.ImageLink.Trim();
                r.StoredImageUrl = null;
                var creditOk = TryNormalizePhotoCredit(r.PhotoCredit, out var credit);
                r.PhotoCredit = credit ?? "";

                var error = QuestionError(r.Question, r.RealAnswer, r.Answer1, r.Answer2, r.Answer3);
                if (error == null && r.ImageLink.Length > 0 && ImportImageClient.DownloadUrl(r.ImageLink) == null)
                    error = "The image_url must be a Google Drive link or a direct link to an image (https://…).";
                if (error == null && !creditOk)
                    error = $"The photo credit is {r.PhotoCredit.Length} characters; the limit is {MaxPhotoCreditLength}.";

                CheckImportRow(r, error, today, dates, taken);
                if (r.Status == "Ready" && r.ImageLink.Length == 0)
                    r.Note = NoImageNote;
            }
        }

        // ---- RaceWord ----

        [HttpGet]
        public async Task<IActionResult> ImportRaceWord(int divisionId)
        {
            var vm = await NewImportAsync<RaceWordImportViewModel>(divisionId);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImportBytes + 100_000)]
        public async Task<IActionResult> PreviewRaceWordImport(int divisionId, IFormFile? file, string? sheetUrl)
        {
            var vm = await NewImportAsync<RaceWordImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            string? text;
            (text, vm.Source, vm.Error) = await ReadImportSourceAsync(file, sheetUrl);
            if (text != null)
            {
                (vm.Rows, vm.Error) = ReadImportRows(CsvTable.Parse(text), RaceWordImportColumns, RaceWordImportRequired,
                    "words", cell => new RaceWordImportRow
                    {
                        Word = cell("word"),
                        Category = cell("category"),
                    });
                if (vm.Error == null)
                {
                    await CheckRaceWordRowsAsync(divisionId, vm.Rows);

                    // Same dictionary check as the create form; a missing word still imports
                    await Parallel.ForEachAsync(vm.Ready, ImportLookups, async (r, _) =>
                    {
                        var status = await WordDictionaryClient.CheckAsync(r.Word.ToLowerInvariant());
                        r.Note = status switch
                        {
                            WordDictionaryStatus.NotFound =>
                                "Not in the dictionary — it's added to the custom dictionary on import so players can guess it.",
                            WordDictionaryStatus.Unverified =>
                                "The dictionary couldn't be checked — if it's missing, it's added to the custom dictionary on import.",
                            _ => null
                        };
                    });
                }
            }

            return View("ImportRaceWord", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportRaceWordRows(int divisionId, string? rowsJson)
        {
            var vm = await NewImportAsync<RaceWordImportViewModel>(divisionId);
            if (vm == null) return NotFound();

            if (!TryReadPreviewRows(rowsJson, vm))
                return View("ImportRaceWord", vm);

            await CheckRaceWordRowsAsync(divisionId, vm.Rows);
            var ready = vm.Ready;
            if (ready.Count > 0)
            {
                await _supabase.From<RaceWordDailyDto>().Insert(ready.Select(r => new RaceWordDailyDto
                {
                    DivisionId = divisionId,
                    GameDate = ImportDate(r),
                    TargetWord = r.Word,
                    Category = NullIfEmpty(r.Category),
                    FunFact = NullIfEmpty(r.FunFact),
                }).ToList());

                foreach (var r in ready)
                    r.Status = "Imported";

                // Like CreateRaceWord: a word in neither dictionary goes into the custom one
                const string notAdded =
                    "Couldn't add it to the custom dictionary — open the day and save it again so players can guess it.";
                await Parallel.ForEachAsync(ready, ImportLookups, async (r, _) =>
                {
                    try
                    {
                        var (status, added) = await MakeWordPlayableAsync(r.Word.ToLowerInvariant());
                        r.Note = status is WordDictionaryStatus.InDictionary or WordDictionaryStatus.InCustom
                            ? null
                            : added ? "Added to the custom dictionary so players can guess it." : notAdded;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        r.Note = notAdded;
                    }
                });
            }

            vm.Imported = ready.Count;
            return View("ImportRaceWord", vm);
        }

        [HttpGet]
        public IActionResult RaceWordImportTemplate()
        {
            var day = DisplayTime.Today.AddDays(1);
            var csv =
                "date,word,category,fun_fact\r\n" +
                $"{day:yyyy-MM-dd},TURBO,Car Component,\"Optional: a fun fact shown under the answer, up to 180 characters\"\r\n" +
                $"{day.AddDays(1):yyyy-MM-dd},APEX,Racing Term,\r\n" +
                $"{day.AddDays(2):yyyy-MM-dd},GRID,,\r\n";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "raceword-import-template.csv");
        }

        // Words follow the create form's rules: 4 to 6 letters A–Z, stored uppercase, and a
        // category of at most 50 characters (blank for none)
        private async Task CheckRaceWordRowsAsync(int divisionId, List<RaceWordImportRow> rows)
        {
            var today = DisplayTime.Today;
            var taken = await GetTakenImportDaysAsync(divisionId, GameModeType.RaceWord);

            var dates = new HashSet<DateTime>();
            foreach (var r in rows)
            {
                var word = WordDictionaryClient.Normalize(r.Word);
                r.Word = word?.ToUpperInvariant() ?? r.Word.Trim();

                string? error = null;
                if (word == null)
                    error = "The word must be 4 to 6 letters, A to Z only.";
                else if (!TryNormalizeCategory(r.Category, out var category))
                    error = $"The category must be {MaxCategoryLength} characters or fewer.";
                else
                    r.Category = category ?? "";

                CheckImportRow(r, error, today, dates, taken);
            }
        }

        // ---- Shared by the three imports ----

        private async Task<TViewModel?> NewImportAsync<TViewModel>(int divisionId)
            where TViewModel : DailyImportViewModel, new()
        {
            var division = (await _supabase.From<RevUpIQ.Admin.Models.Division.DivisionDto>()
                .Where(d => d.Id == divisionId)
                .Get()).Models.FirstOrDefault();

            return division == null
                ? null
                : new TViewModel { DivisionId = divisionId, DivisionName = division.Name };
        }

        // The text of the uploaded CSV file or of the shared Google Sheet, and the name the page shows for it
        private static async Task<(string? Text, string? Source, string? Error)> ReadImportSourceAsync(
            IFormFile? file, string? sheetUrl)
        {
            if (file != null && file.Length > 0)
            {
                if (file.Length > MaxImportBytes)
                    return (null, file.FileName, "That file is too big. Split it into files under 2 MB.");

                using var reader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8, true);
                return (await reader.ReadToEndAsync(), file.FileName, null);
            }

            if (!string.IsNullOrWhiteSpace(sheetUrl))
            {
                var (text, error) = await DownloadGoogleSheetAsync(sheetUrl.Trim());
                return (text, "Google Sheet", error);
            }

            return (null, null, "Choose a CSV file or paste a Google Sheet link.");
        }

        // The ready rows the preview page posted back; false (with the page's error set) if unreadable
        private static bool TryReadPreviewRows<TRow>(string? rowsJson, DailyImportViewModel<TRow> vm)
            where TRow : DailyImportRow
        {
            try
            {
                vm.Rows = System.Text.Json.JsonSerializer.Deserialize<List<TRow>>(rowsJson ?? "") ?? new();
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                vm.Error = "The preview couldn't be read. Preview the file again.";
                return false;
            }
        }

        // Maps the header to fields and returns one row per non-blank line. readRow builds the
        // game's row from cell(field); the date and fun fact are read here. A missing optional
        // column reads as "".
        private static (List<TRow> Rows, string? Error) ReadImportRows<TRow>(
            List<string[]> table,
            Dictionary<string, string> headers,
            (string Field, string Header)[] required,
            string noun,
            Func<Func<string, string>, TRow> readRow) where TRow : DailyImportRow
        {
            var rows = new List<TRow>();
            if (table.Count == 0)
                return (rows, "The file is empty.");

            var columns = new Dictionary<string, int>();
            for (var i = 0; i < table[0].Length; i++)
            {
                var key = new string(table[0][i].Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (headers.TryGetValue(key, out var field))
                    columns.TryAdd(field, i);
            }

            var missing = required.Where(c => !columns.ContainsKey(c.Field)).Select(c => c.Header).ToList();
            if (missing.Count > 0)
                return (rows, $"The first row must name the columns. Missing: {string.Join(", ", missing)}. " +
                              "Download the template to see the layout.");

            for (var i = 1; i < table.Count; i++)
            {
                var cells = table[i];
                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;

                string Cell(string field) =>
                    columns.TryGetValue(field, out var c) && c < cells.Length ? cells[c].Trim() : "";

                var row = readRow(Cell);
                row.Row = i + 1;
                row.Date = Cell("date");
                row.FunFact = Cell("funfact");
                rows.Add(row);
            }

            if (rows.Count == 0)
                return (rows, $"The file has a header but no {noun}.");
            if (rows.Count > MaxImportRows)
                return (new List<TRow>(), $"The file has {rows.Count} {noun}; import at most {MaxImportRows} at a time.");

            return (rows, null);
        }

        // Sets the row's Status and Note from the checks every import shares, in order: the
        // date, the game's own fields (fieldError, null when they're fine), the fun fact, one
        // row per day, and a day that already has a question (Skipped)
        private static void CheckImportRow(DailyImportRow r, string? fieldError, DateTime today,
            HashSet<DateTime> dates, HashSet<DateTime> taken)
        {
            r.Status = "Error";
            r.FunFact = r.FunFact.Replace("\r\n", "\n").Trim();

            if (!DateTime.TryParseExact(r.Date.Trim(), ImportDateFormats, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                r.Note = "The date must look like 2026-11-05 or 11/5/2026.";
                return;
            }

            r.Date = date.ToString("yyyy-MM-dd");

            if (date < today)
                r.Note = "The date is in the past.";
            else if (fieldError != null)
                r.Note = fieldError;
            else if (r.FunFact.Length > MaxFunFactLength)
                r.Note = $"The fun fact is {r.FunFact.Length} characters; the limit is {MaxFunFactLength}.";
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

        // Null when a trivia or Photo Guess question and its four answers are fine
        private static string? QuestionError(string question, string real, string wrong1, string wrong2, string wrong3)
        {
            var answers = new[] { real, wrong1, wrong2, wrong3 };

            if (question.Length == 0)
                return "The question is empty.";
            if (answers.Any(a => a.Length == 0))
                return "Every question needs a correct answer and three wrong answers.";
            if (answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() < 4)
                return "Two of the answers are the same.";
            return null;
        }

        // Days from today on that already have this game's question for the division
        private async Task<HashSet<DateTime>> GetTakenImportDaysAsync(int divisionId, GameModeType mode)
        {
            var from = DisplayTime.Today.ToString("yyyy-MM-dd");

            return mode switch
            {
                GameModeType.Trivia => (await _supabase.From<TriviaDailyDto>()
                        .Select("id,game_date")
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, from)
                        .Get())
                    .Models.Select(t => t.GameDate.Date).ToHashSet(),

                GameModeType.PhotoGuess => (await _supabase.From<PhotoGuessDailyDto>()
                        .Select("id,game_date")
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, from)
                        .Get())
                    .Models.Select(t => t.GameDate.Date).ToHashSet(),

                GameModeType.RaceWord => (await _supabase.From<RaceWordDailyDto>()
                        .Select("id,game_date")
                        .Filter("division_id", Operator.Equals, divisionId)
                        .Filter("game_date", Operator.GreaterThanOrEqual, from)
                        .Get())
                    .Models.Select(t => t.GameDate.Date).ToHashSet(),

                _ => new HashSet<DateTime>()
            };
        }

        private static DateTime ImportDate(DailyImportRow r) =>
            DateTime.ParseExact(r.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

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
            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);
            if (!TryNormalizePhotoCredit(model.PhotoCredit, out var photoCredit))
                return BadRequest(PhotoCreditTooLong);

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
                FunFact = funFact,
                PhotoCredit = photoCredit,
                ImageUrl = imageUrl
            };

            await _supabase.From<PhotoGuessDailyDto>().Insert(photoGuess);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePhotoGuess(long id, PhotoGuessItem model, IFormFile? ImageFile)
        {
            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);
            if (!TryNormalizePhotoCredit(model.PhotoCredit, out var photoCredit))
                return BadRequest(PhotoCreditTooLong);

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
            existing.FunFact = funFact;
            existing.PhotoCredit = photoCredit;

            // A question without an image (e.g. from a bulk import) is published once one is added here
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
            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);

            var raceWord = new RaceWordDailyDto
            {
                DivisionId = DivisionId,
                GameDate = gameDate,
                TargetWord = model.TargetWord,
                Category = category,
                FunFact = funFact,
            };

            await _supabase.From<RaceWordDailyDto>().Insert(raceWord);

            return Json(await EnsureWordIsPlayableAsync(model.TargetWord));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRaceWord(long id, WordleWord model)
        {
            if (!TryNormalizeCategory(model.Category, out var category))
                return BadRequest($"Category must be {MaxCategoryLength} characters or fewer.");
            if (!TryNormalizeFunFact(model.FunFact, out var funFact))
                return BadRequest(FunFactTooLong);

            var existing = await _supabase
                .From<RaceWordDailyDto>()
                .Where(x => x.Id == id)
                .Single();

            if (existing == null)
                return NotFound();

            existing.TargetWord = model.TargetWord;
            existing.Category = category;
            existing.FunFact = funFact;
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

            var (status, added) = await MakeWordPlayableAsync(w);

            if (status is WordDictionaryStatus.InDictionary or WordDictionaryStatus.InCustom)
                return new { addedToDictionary = false, status = status.ToString() };

            return new { addedToDictionary = added, status = status.ToString(), word = w };
        }

        // Checks a normalized word and adds it to the custom dictionary when it's in neither
        // dictionary (also used by the RaceWord import)
        private static async Task<(WordDictionaryStatus Status, bool Added)> MakeWordPlayableAsync(string word)
        {
            var status = await WordDictionaryClient.CheckAsync(word);

            if (status is WordDictionaryStatus.InDictionary or WordDictionaryStatus.InCustom)
                return (status, false);

            return (status, await WordDictionaryClient.AddToCustomAsync(word));
        }

        #endregion
    }
}

