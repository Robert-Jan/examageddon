namespace Examageddon.Data.Entities;

public class SessionAnswer
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ExamSession Session { get; set; } = null!;

    public int QuestionId { get; set; }

    public Question Question { get; set; } = null!;

    public double Score { get; set; }

    public bool IsCorrect { get; set; }

    public DateTime AnsweredAt { get; set; }

    public ICollection<SessionAnswerSelection> Selections { get; set; } = [];
}
