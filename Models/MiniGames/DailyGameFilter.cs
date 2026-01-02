namespace RevUpIQ.Admin.Models.MiniGames;



public struct DailyGameFilter
{
    public int Id { get; set; }
    public string Date { get; set; }
    public GameModeType Mode { get; set; }

    public DailyGameFilter(int id, GameModeType mode,string date)
    {
        Id = id;
        Date = date;
        Mode = mode;
    }
}