namespace RevUpIQ.Admin.Models.MiniGames
{
    // One question from a trivia import file (CSV upload or Google Sheet)
    public class TriviaImportRow : DailyImportRow
    {
        public string Question { get; set; } = "";
        public string RealAnswer { get; set; } = "";
        public string Answer1 { get; set; } = "";
        public string Answer2 { get; set; } = "";
        public string Answer3 { get; set; } = "";
    }

    public class TriviaImportViewModel : DailyImportViewModel<TriviaImportRow>
    {
    }
}
