using System.Collections.Generic;

namespace RevUpIQ.Admin.Models.Users
{
    public class UsersListViewModel
    {
        public List<AuthUser> Users { get; set; } = new();
        public int Page { get; set; }
        public int PerPage { get; set; }
        public bool HasNextPage { get; set; }
    }
}
