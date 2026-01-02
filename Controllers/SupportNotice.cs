using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("support_notice")]
public class SupportNotice : BaseModel
{
    [PrimaryKey("id", false)]
    public bool Id { get; set; }

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
