using Microsoft.AspNetCore.Mvc;
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

        public async Task<IActionResult> Index()
        {
            var sponsorId = await GetActiveSponsorBarId(null, null);

            SponsorBarEntry model;

            if (sponsorId.HasValue)
                model = await GetSponsorBarById(sponsorId.Value) ?? new SponsorBarEntry();
            else
                model = new SponsorBarEntry();

            var now = DateTime.UtcNow;
            var weekStatus = await WeeksStatus(now.Year);
            ViewBag.WeekStatus = weekStatus;

            return View("~/Views/Sponsor/SponsorBar.cshtml", model);
        }

        public async Task<IActionResult> ByDate(DateTime date)
        {
            var year = date.Year;
            var week = ISOWeek.GetWeekOfYear(date);

            var sponsorId = await GetActiveSponsorBarId(year, week);

            SponsorBarEntry model;

            if (sponsorId.HasValue)
                model = await GetSponsorBarById(sponsorId.Value) ?? new SponsorBarEntry { Year = year, Week_Number = week };
            else
                model = new SponsorBarEntry { Year = year, Week_Number = week };


            var now = DateTime.UtcNow;
            var weekStatus = await WeeksStatus(year);
            ViewBag.WeekStatus = weekStatus;

            return View("~/Views/Sponsor/SponsorBar.cshtml", model);
        }


        [HttpPost]
        public async Task<IActionResult> Edit(Guid sponsorId, int Year, int Week_Number, string Text, string Link_Url, IFormFile? ImageFile)
        {
            var all = await _supabase.From<SponsorBarEntry>().Get();
            var entry = all.Models.FirstOrDefault(x => x.Sponsor_Id == sponsorId);

            if (entry == null)
                return RedirectToAction(nameof(ByDate), new { date = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday) });

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

            return RedirectToAction(nameof(ByDate), new { date = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday) });
        }

        [HttpPost]
        public async Task<IActionResult> Create(int Year, int Week_Number, string Text, string Link_Url, IFormFile? ImageFile)
        {
            var existingId = await GetActiveSponsorBarId(Year, Week_Number);

            if (existingId.HasValue)
            {
                var resExisting = await _supabase
                    .From<SponsorBarEntry>()
                    .Filter("sponsor_id", Supabase.Postgrest.Constants.Operator.Equals, existingId.Value.ToString())
                    .Get();

                var existing = resExisting.Models.FirstOrDefault();
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
                    return RedirectToAction(nameof(ByDate), new { date = dateExisting });
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
                Week_Number = Week_Number
            };

            await _supabase.From<SponsorBarEntry>().Insert(entry);

            var dateNew = ISOWeek.ToDateTime(Year, Week_Number, DayOfWeek.Monday);
            return RedirectToAction(nameof(ByDate), new { date = dateNew });
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
        public async Task<List<bool>> WeeksStatus(int year)
        {
            var result = await _supabase
                .Rpc<List<SponsorWeekStatus>>(
                    "get_sponsor_weeks_status",
                    new Dictionary<string, object>
                    {
                        ["p_year"] = year
                    }
                );

            return result.Select(x => x.Has_Data).ToList();
        }

        #endregion

    }

}
