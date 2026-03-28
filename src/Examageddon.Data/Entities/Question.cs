using Examageddon.Data.Enums;

namespace Examageddon.Data.Entities;

public class Question
{
    public int Id { get; set; }

    public int ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

    public byte[]? ImageData { get; set; }

    public string? ImageContentType { get; set; }

    public int OrderIndex { get; set; }

    public bool AllowMultipleAnswers { get; set; }

    public MultiAnswerScoringMode ScoringMode { get; set; }

    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];
}
