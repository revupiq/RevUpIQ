namespace RevUpIQ.Admin.Models.MiniGames
{
    // One word from a RaceWord import file (CSV upload or Google Sheet)
    public class RaceWordImportRow : DailyImportRow
    {
        public string Word { get; set; } = "";        // uppercase once checked
        public string Category { get; set; } = "";    // empty means no category
    }

    public class RaceWordImportViewModel : DailyImportViewModel<RaceWordImportRow>
    {
    }
}
