namespace Examageddon.Services.Models;

public class HistoryEntryModel
{
    public int SessionId { get; set; }

    public string ExamTitle { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }

    public int TotalQuestions { get; set; }

    public double CorrectAnswers { get; set; }

    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round(CorrectAnswers / TotalQuestions * 100, 1);

    public bool IsPassed { get; set; }
}
