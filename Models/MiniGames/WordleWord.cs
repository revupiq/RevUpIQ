namespace RevUpIQ.Admin.Models.MiniGames
{
    public class WordleWord
    {
        public string TargetWord  { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? FunFact { get; set; }      // optional; empty saves null
    }
}
