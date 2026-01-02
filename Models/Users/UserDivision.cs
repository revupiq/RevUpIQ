using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Users
{
    [Table("user_divisions")]
    public class UserDivision : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public Guid User_Id { get; set; }

        [Column("division_id")]
        public int Division_Id { get; set; }

        [Column("subscribed_at")]
        public DateTime Subscribed_At { get; set; }
    }
}


namespace RevUpIQ.Admin.Models.Users
{
    public class UserGameHistoryViewModel
    {
        public List<UserGamePlay> Games { get; set; } = new();
        public List<RevUpIQ.Admin.Models.Division.DivisionDto> Divisions { get; set; } = new();
    }
}
