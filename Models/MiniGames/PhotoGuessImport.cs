using System.Text.Json.Serialization;

namespace RevUpIQ.Admin.Models.MiniGames
{
    // One question from a Photo Guess import file (CSV upload or Google Sheet)
    public class PhotoGuessImportRow : DailyImportRow
    {
        public string Question { get; set; } = "";
        public string RealAnswer { get; set; } = "";
        public string Answer1 { get; set; } = "";
        public string Answer2 { get; set; } = "";
        public string Answer3 { get; set; } = "";

        // The image_url cell: a Google Drive or direct image link, or empty for a question
        // that stays unpublished until the admin adds the image
        public string ImageLink { get; set; } = "";

        // The photo_credit cell; optional, shown under the photo in the app
        public string PhotoCredit { get; set; } = "";

        // The copy saved in the photoguess bucket while importing; never read from the form
        [JsonIgnore]
        public string? StoredImageUrl { get; set; }
    }

    public class PhotoGuessImportViewModel : DailyImportViewModel<PhotoGuessImportRow>
    {
    }
}
