namespace Examageddon.Data.Entities;

public class SessionQuestion
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ExamSession Session { get; set; } = null!;

    public int QuestionId { get; set; }

    public Question Question { get; set; } = null!;

    public int Position { get; set; }
}
