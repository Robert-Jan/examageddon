using Examageddon.Data.Entities;

namespace Examageddon.Services.Models;

public class QuestionResultItem
{
    public Question Question { get; set; } = null!;

    public IReadOnlyList<AnswerOption> SelectedOptions { get; set; } = [];

    public bool IsCorrect { get; set; }

    public bool IsAnswered { get; set; }

    public IReadOnlyDictionary<int, AnswerOption> DragPlacements { get; set; } = new Dictionary<int, AnswerOption>();
}
