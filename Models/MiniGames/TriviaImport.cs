namespace RevUpIQ.Admin.Models.MiniGames
{
    // One question from a trivia import file (CSV upload or Google Sheet)
    public class TriviaImportRow
    {
        public int Row { get; set; }                  // spreadsheet row number; row 1 is the header
        public string Date { get; set; } = "";        // yyyy-MM-dd once checked, else as typed
        public string Question { get; set; } = "";
        public string RealAnswer { get; set; } = "";
        public string Answer1 { get; set; } = "";
        public string Answer2 { get; set; } = "";
        public string Answer3 { get; set; } = "";

        // "Ready", "Skipped" (the day already has a question), "Error" or "Imported"
        public string Status { get; set; } = "Ready";
        public string? Note { get; set; }
    }

    public class TriviaImportViewModel
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = "";
        public string? Source { get; set; }           // file name or "Google Sheet"
        public string? Error { get; set; }            // the file couldn't be read at all
        public List<TriviaImportRow> Rows { get; set; } = new();
        public int? Imported { get; set; }            // set after Import

        public List<TriviaImportRow> Ready => Rows.Where(r => r.Status == "Ready").ToList();
        public int Count(string status) => Rows.Count(r => r.Status == status);
    }
}
