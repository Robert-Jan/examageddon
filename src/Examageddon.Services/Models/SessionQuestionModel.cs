using Examageddon.Data.Entities;
using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class SessionQuestionModel
{
    public int SessionId { get; set; }

    public int Position { get; set; }

    public int TotalQuestions { get; set; }

    public FeedbackMode FeedbackMode { get; set; }

    public Question Question { get; set; } = null!;

    public List<AnswerOption> AnswerOptions { get; set; } = [];

    public IReadOnlySet<int> SelectedAnswerOptionIds { get; set; } = new HashSet<int>();

    public bool IsAnswered { get; set; }

    public bool AllowMultipleAnswers { get; set; }

    public QuestionType QuestionType { get; set; }

    public bool IsFromReview { get; set; }

    public IReadOnlyDictionary<int, int> DragPlacements { get; set; } = new Dictionary<int, int>();
}
