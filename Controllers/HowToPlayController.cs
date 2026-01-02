using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models;
using Supabase;

namespace RevUpIQ.Admin.Controllers
{
    public class HowToPlayController : Controller
    {
        private readonly Client _supabase;

        public HowToPlayController(Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            var result = await _supabase
                .From<HowToPlay>()
                .Select("*")
                .Limit(1)
                .Get();

            var model = result.Models.FirstOrDefault() ?? new HowToPlay();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(HowToPlay model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            if (model.Id <= 0)
            {
                await _supabase.From<HowToPlay>().Insert(model);
            }
            else
            {
                await _supabase
                    .From<HowToPlay>()
                    .Where(x => x.Id == model.Id)
                    .Set(x => x.Video_Link, model.Video_Link)
                    .Set(x => x.Trivia_Text, model.Trivia_Text)
                    .Set(x => x.Photo_Guess_Text, model.Photo_Guess_Text)
                    .Set(x => x.Race_Word_Text, model.Race_Word_Text)
                    .Update();
            }

            return Ok();
        }

    }
}
