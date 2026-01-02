using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Sponsors;

namespace RevUpIQ.Admin.Controllers
{
    public class SponsorsRequestsController : Controller
    {
        private readonly Supabase.Client _supabase;

        public SponsorsRequestsController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var response = await _supabase
                .From<BusinessInquiry>()
                .Order(x => x.Created_At, Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            var inquiries = response.Models;

            return View("~/Views/Sponsor/SponsorsRequests.cshtml", inquiries);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest();

            await _supabase
                .From<BusinessInquiry>()
                .Where(x => x.Id == id)
                .Delete();

            return Ok();
        }
    }
}
