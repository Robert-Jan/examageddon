using Examageddon.Data.Enums;

namespace Examageddon.Data.Entities;

public class Exam
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int PassingThreshold { get; set; }

    public int ExamQuestionCount { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public ICollection<Question> Questions { get; set; } = [];

    public ICollection<ExamSession> Sessions { get; set; } = [];
}
