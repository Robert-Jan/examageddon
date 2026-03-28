namespace Examageddon.Services.Models;

public class StagedOption
{
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public string? MatchText { get; set; }
}
