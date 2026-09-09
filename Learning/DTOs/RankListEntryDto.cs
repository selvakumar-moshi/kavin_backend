namespace LearningBackendAPI.DTOs
{
    public class RankListEntryDto
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string ProfileImage { get; set; } = "";
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public string Score { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
    }
}
