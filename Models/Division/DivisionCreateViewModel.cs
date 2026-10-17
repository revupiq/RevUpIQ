using Microsoft.AspNetCore.Http;

namespace RevUpIQ.Admin.Models.Division
{
    public class DivisionCreateViewModel
    {
        public string Name { get; set; } = string.Empty;
        public IFormFile? Logo { get; set; }
        public IFormFile? PurchaseIcon { get; set; }
        public string? GoogleProductId { get; set; }
        public string? AppleProductIdMonthly { get; set; }
        public string? AppleProductIdAnnual { get; set; }
    }
}

namespace RevUpIQ.Admin.Models.Users
{
    public class UserDivisionViewModel
    {
        public UserDivision UserDivision { get; set; } = null!;
        public RevUpIQ.Admin.Models.Division.DivisionDto? Division { get; set; }
    }
}
