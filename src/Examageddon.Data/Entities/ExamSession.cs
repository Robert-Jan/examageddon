using Examageddon.Data.Enums;

namespace Examageddon.Data.Entities;

public class ExamSession
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public Person Person { get; set; } = null!;

    public int ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public QuestionMode QuestionMode { get; set; }

    public OrderMode OrderMode { get; set; }

    public FeedbackMode FeedbackMode { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public int TotalQuestions { get; set; }

    public double CorrectAnswers { get; set; }

    public bool? IsPassed { get; set; }

    public ICollection<SessionQuestion> SessionQuestions { get; set; } = [];

    public ICollection<SessionAnswer> SessionAnswers { get; set; } = [];
}
