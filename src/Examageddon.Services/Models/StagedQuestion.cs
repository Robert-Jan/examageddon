using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class StagedQuestion
{
    public QuestionType QuestionType { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool AllowMultipleAnswers { get; set; }

    public MultiAnswerScoringMode ScoringMode { get; set; }

    public List<StagedOption> Options { get; set; } = [];
}
