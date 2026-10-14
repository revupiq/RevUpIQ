using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.Sponsors;
using System.Collections.Generic;
using System.Globalization;

namespace RevUpIQ.Admin.Controllers
{
    public class SponsorBarController : Controller
    {
        public class SponsorWeekStatus
        {
            public int Week_Number { get; set; }
            public bool Has_Data { get; set; }
        }

        private readonly Supabase.Client _supabase;

        public SponsorBarController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        // divisionId: null = the ad shown in every division; otherwise that division's own ad
        public async Task<IActionResult> Index(long? divisionId = null)
        {
            var now = DateTime.UtcNow;
            return await ShowWeek(now.Year, ISOWeek.GetWeekOfYear(now), divisionId);
        }

        public async Task<IActionResult> ByDate(DateTime date, long? divisionId = null)
        {
            return await ShowWeek(date.Year, ISOWeek.GetWeekOfYear(date), divisionId);
        }

        private async Task<IActionResult> ShowWeek(int year, int week, long? divisionId)
        {
            // Exactly this scope: the admin edits this division's own ad, not the fallback
            var model = await FindEntry(year, week, divisionId)
                        ?? new SponsorBarEntry { Year = year, Week_Number = week, Division_Id = divisionId };

            ViewBag.WeekStatus = await WeeksStatus(year, divisionId);
            ViewBag.DivisionId = divisionId;
            ViewBag.Divisions = await LoadDivisions();
            ViewBag.FallbackExists = divisionId.HasValue && model.Sponsor_Id == Guid.Empty
                && await FindEntry(year, week, null) != null;

            return View("~/Views/Sponsor/SponsorBar.cshtml", model);
        }

        private async Task<List<DivisionDto>> LoadDivisions()
        {
            var result = await _supabase.From<DivisionDto>().Get();
            return result.Models.OrderBy(d => d.SortIndex).ThenBy(d => d.Id).ToList();
        }

        private async Task<SponsorBarEntry?> FindEntry(int year, int week, long? divisionId)
        {
            var rows = await _supabase
                .From<SponsorBarEntry>()
                .Filter("year", Supabase.Postgrest.Constants.Operator.Equals, year)
                .Get();

            return rows.Models.FirstOrDefault(x => x.Week_Number == week && x.Division_Id == divisionId);
        }


        [HttpPost]
        public async Task<IActionResult> Edit(Guid sponsorId, int Year, int Week_Number, string Text, string Link_Url, IFormFile? ImageFile, long? DivisionId = null)
        {
            var all = await _supabase.From<SponsorBarEntry>().Get();
            var entry = all.Models.FirstOrDefault(x => x.Sponsor_Id == sponsorId);

            if (entry == null)
                return RedirectToAction(nameof(ByDate), new { date = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday), divisionId = DivisionId });

            var imageUrl = entry.Image_Url;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = $"sponsor_{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";

                using var ms = new MemoryStream();
                await ImageFile.CopyToAsync(ms);
                var bytes = ms.ToArray();

                await _supabase.Storage.From("sponsor-bar").Upload(bytes, fileName);
                imageUrl = _supabase.Storage.From("sponsor-bar").GetPublicUrl(fileName);
            }

            entry.Text = Text ?? "";
            entry.Link_Url = Link_Url ?? "";
            entry.Image_Url = imageUrl;
            entry.Year = Year;
            entry.Week_Number = Week_Number;

            await _supabase.From<SponsorBarEntry>().Update(entry);

            return RedirectToAction(nameof(ByDate), new { date = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday), divisionId = entry.Division_Id });
        }

        [HttpPost]
        public async Task<IActionResult> Create(int Year, int Week_Number, string Text, string Link_Url, IFormFile? ImageFile, long? DivisionId = null)
        {
            // Only this scope counts as "already exists" (a division may override the all-divisions ad)
            {
                var existing = await FindEntry(Year, Week_Number, DivisionId);
                if (existing != null)
                {
                    var imageUrl = existing.Image_Url;

                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        var fileName = $"sponsor_{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";

                        using var ms = new MemoryStream();
                        await ImageFile.CopyToAsync(ms);
                        var bytes = ms.ToArray();

                        await _supabase.Storage.From("sponsor-bar").Upload(bytes, fileName);
                        imageUrl = _supabase.Storage.From("sponsor-bar").GetPublicUrl(fileName);
                    }

                    existing.Text = Text ?? "";
                    existing.Link_Url = Link_Url ?? "";
                    existing.Image_Url = imageUrl;
                    existing.Year = Year;
                    existing.Week_Number = Week_Number;

                    await _supabase.From<SponsorBarEntry>().Update(existing);

                    var dateExisting = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday);
                    return RedirectToAction(nameof(ByDate), new { date = dateExisting, divisionId = DivisionId });
                }
            }

            var newImageUrl = "";

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = $"sponsor_{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";

                using var ms = new MemoryStream();
                await ImageFile.CopyToAsync(ms);
                var bytes = ms.ToArray();

                await _supabase.Storage.From("sponsor-bar").Upload(bytes, fileName);
                newImageUrl = _supabase.Storage.From("sponsor-bar").GetPublicUrl(fileName);
            }

            var entry = new SponsorBarEntry
            {
                Sponsor_Id = Guid.NewGuid(),
                Text = Text ?? "",
                Link_Url = Link_Url ?? "",
                Image_Url = newImageUrl,
                Year = Year,
                Week_Number = Week_Number,
                Division_Id = DivisionId
            };

            await _supabase.From<SponsorBarEntry>().Insert(entry);

            var dateNew = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday);
            return RedirectToAction(nameof(ByDate), new { date = dateNew, divisionId = DivisionId });
        }



        #region Load Suponsur Bar

        public async Task<Guid?> GetActiveSponsorBarId(int? year, int? week)
        {
            var now = DateTime.UtcNow;
            var y = year ?? now.Year;
            var w = week ?? ISOWeek.GetWeekOfYear(now);

            return await _supabase.Rpc<Guid?>(
                "get_active_sponsor_bar_id",
                new Dictionary<string, object>
                {
                    ["p_year"] = y,
                    ["p_week"] = w
                }
            );
        }


        public async Task<SponsorBarEntry?> GetSponsorBarById(Guid sponsorId)
        {
            var res = await _supabase
                .From<SponsorBarEntry>()
                .Filter("sponsor_id", Supabase.Postgrest.Constants.Operator.Equals, sponsorId.ToString())
                .Get();

            return res.Models.FirstOrDefault();
        }

        [HttpGet]
        public async Task<List<bool>> WeeksStatus(int year, long? divisionId = null)
        {
            var args = new Dictionary<string, object> { ["p_year"] = year };
            if (divisionId.HasValue)
                args["p_division_id"] = divisionId.Value;

            var result = await _supabase
                .Rpc<List<SponsorWeekStatus>>("get_sponsor_weeks_status", args);

            return result.Select(x => x.Has_Data).ToList();
        }

        #endregion

    }

}
