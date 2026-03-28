namespace Examageddon.Data.Entities;

public class SessionAnswerSelection
{
    public int Id { get; set; }

    public int SessionAnswerId { get; set; }

    public SessionAnswer SessionAnswer { get; set; } = null!;

    public int AnswerOptionId { get; set; }

    public AnswerOption AnswerOption { get; set; } = null!;

    public int? PlacedOptionId { get; set; }
}
