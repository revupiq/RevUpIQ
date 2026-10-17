using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace RevUpIQ.Admin.Models.Leagues
{
    // Only the columns the admin needs to clean up a deleted player's invites
    [Table("league_invites")]
    public class LeagueInviteDto : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("inviter_id")]
        public Guid InviterId { get; set; }

        [Column("invitee_id")]
        public Guid InviteeId { get; set; }
    }
}
