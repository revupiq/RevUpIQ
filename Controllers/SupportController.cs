using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Controllers;
using RevUpIQ.Admin.Models.Users;
using Supabase;
using YourApp.Models.Support;


namespace YourApp.Controllers
{
    public sealed class SupportController : Controller
    {
        private readonly Client _supabase;

        public SupportController(Client supabase)
        {
            _supabase = supabase;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tickets = (await _supabase
                .From<Ticket>()
                .Where(t => t.Status == "open")
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get()).Models.ToList();

            var userController = new UsersController(_supabase);

            var users = new Dictionary<Guid, UserProfileViewModel?>();
            foreach (var uid in tickets.Select(t => t.UserId).Distinct())
                users[uid] = await userController.LoadUserProfileAsync(uid.ToString());

            var unreadCounts = new Dictionary<Guid, int>();
            foreach (var t in tickets)
                unreadCounts[t.Id] = await UnreadUserMessageCount(t.Id);

            var model = new SupportTicketsIndexViewModel
            {
                Tickets = tickets,
                Users = users,
                UnreadUserCounts = unreadCounts
            };

            return View(model);
        }

        #region Categories
        [HttpGet]
        public async Task<IActionResult> CategoriesDropdown()
        {
            var categoriesTask = _supabase
                .From<SupportCategory>()
                .Order("sort_order", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var noticeTask = _supabase
                .From<SupportNotice>()
                .Select("message")
                .Single();

            await Task.WhenAll(categoriesTask, noticeTask);

            ViewBag.SupportNotice = noticeTask.Result?.Message;

            return PartialView("_CategoriesDropdown", categoriesTask.Result.Models);
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest();

            await _supabase
                .From<SupportCategory>()
                .Insert(new SupportCategory
                {
                    Name = name.Trim(),
                    SortOrder = 999
                });

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> EditCategory(Guid id, string name)
        {
            if (id == Guid.Empty || string.IsNullOrWhiteSpace(name))
                return BadRequest();

            await _supabase
                .From<SupportCategory>()
                .Where(c => c.Id == id)
                .Set(c => c.Name, name.Trim())
                .Update();

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest();

            await _supabase
                .From<SupportCategory>()
                .Where(c => c.Id == id)
                .Delete();

            return Ok();
        }

        #endregion

        [HttpPost]
        public async Task<IActionResult> UpdateSupportNotice([FromBody] string message)
        {
            await _supabase
                .From<SupportNotice>()
                .Update(new SupportNotice
                {
                    Id = true,
                    Message = message
                });

            return Ok();
        }


        [HttpGet]
        public async Task<IActionResult> Ticket(Guid id)
        {

            var ticket = await _supabase
                .From<Ticket>()
                .Where(t => t.Id == id)
                .Single();

            var messages = (await _supabase
                .From<TicketMessage>()
                .Where(m => m.TicketId == id)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get()).Models.ToList();

            var userController = new UsersController(_supabase);
            var userProfile = await userController.LoadUserProfileAsync(ticket.UserId.ToString());

            var model = new SupportTicketViewModel
            {
                Ticket = ticket,
                Messages = messages,
                UserProfile = userProfile
            };

            await MarkUserMessagesAsRead(id);
            return View("Ticket", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(Guid id, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return RedirectToAction(nameof(Ticket), new { id });

            await _supabase
                .From<TicketMessage>()
                .Insert(new TicketMessage
                {
                    TicketId = id,
                    SenderId = null,
                    SenderRole = "admin",
                    Body = body.Trim(),
                    CreatedAt = DateTime.UtcNow
                });

            return RedirectToAction(nameof(Ticket), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(Guid id)
        {
            await _supabase
                .From<Ticket>()
                .Where(t => t.Id == id)
                .Set(t => t.Status!, "closed")
                .Update();

            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public async Task<int> UnreadUserMessageCount(Guid id)
        {
            var res = await _supabase
                .From<TicketMessage>()
                .Filter("ticket_id", Supabase.Postgrest.Constants.Operator.Equals, id.ToString())
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Filter("sender_role", Supabase.Postgrest.Constants.Operator.Equals, "user")
                .Get();

            return res.Models.Count;
        }

        [HttpPost]
        public async Task<IActionResult> MarkUserMessagesAsRead(Guid id)
        {
            await _supabase
                .From<TicketMessage>()
                .Filter("ticket_id", Supabase.Postgrest.Constants.Operator.Equals, id.ToString())
                .Filter("sender_role", Supabase.Postgrest.Constants.Operator.Equals, "user")
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Set(m => m.Is_Read, true)
                .Update();

            return Ok();
        }
    }
}
