using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace RevUpIQ.Admin.Models.Leagues
{
    [Table("leagues")]
    public class LeagueDto : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("password")]
        public string? Password { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("max_players")]
        public int? MaxPlayers { get; set; }

        [Column("division_id")]
        public int? DivisionId { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("joined_users")]
        public string[] JoinedUsers { get; set; } = Array.Empty<string>();

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
