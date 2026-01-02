namespace RevUpIQ.Admin.Models.Sponsors
{
    public class SponsorBarItem
    {
        public string ImageUrl { get; set; } = "";
        public string LinkUrl { get; set; } = "";
        public string Text { get; set; } = "";

        public int Year { get; set; }
        public int Week_Number { get; set; }
    }
}
