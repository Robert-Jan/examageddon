using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class SessionOptions
{
    public QuestionMode QuestionMode { get; set; }

    public OrderMode OrderMode { get; set; }

    public FeedbackMode FeedbackMode { get; set; }
}
