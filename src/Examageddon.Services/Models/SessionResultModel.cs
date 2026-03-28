using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class SessionResultModel
{
    public int SessionId { get; set; }

    public string ExamTitle { get; set; } = string.Empty;

    public int TotalQuestions { get; set; }

    public double CorrectAnswers { get; set; }

    public int PassingThreshold { get; set; }

    public bool IsPassed { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round(CorrectAnswers / TotalQuestions * 100, 1);

    public int ScaledScore => TotalQuestions == 0 ? 0 : (int)Math.Round(CorrectAnswers / TotalQuestions * 1000);

    public List<QuestionResultItem> QuestionResults { get; set; } = [];
}
