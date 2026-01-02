using RevUpIQ.Admin.Models.MiniGames;
using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Models.MiniGames
{
    public class CombinedSuggestion
    {
        public RaceWordSubmission RaceWord { get; set; }
        public TriviaSubmission Trivia { get; set; }
        public PhotoGuessSubmission PhotoGuess { get; set; }

        public UserProfileViewModel UserProfile { get; set; }
    }
}
