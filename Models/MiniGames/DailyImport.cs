namespace RevUpIQ.Admin.Models.MiniGames
{
    // One day's question from an import file (CSV upload or Google Sheet). The trivia,
    // Photo Guess and RaceWord rows add their own fields.
    public class DailyImportRow
    {
        public int Row { get; set; }                  // spreadsheet row number; row 1 is the header
        public string Date { get; set; } = "";        // yyyy-MM-dd once checked, else as typed
        public string FunFact { get; set; } = "";     // optional; empty imports as none

        // "Ready", "Skipped" (the day already has a question), "Error" or "Imported"
        public string Status { get; set; } = "Ready";
        public string? Note { get; set; }             // why, or a warning on a Ready row
    }

    // The import page for one division
    public class DailyImportViewModel
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = "";
        public string? Source { get; set; }           // file name or "Google Sheet"
        public string? Error { get; set; }            // the file couldn't be read at all
        public int? Imported { get; set; }            // set after Import
    }

    public class DailyImportViewModel<TRow> : DailyImportViewModel where TRow : DailyImportRow
    {
        public List<TRow> Rows { get; set; } = new();

        public List<TRow> Ready => Rows.Where(r => r.Status == "Ready").ToList();
        public int Count(string status) => Rows.Count(r => r.Status == status);
    }
}
