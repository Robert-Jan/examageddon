using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Data.Repositories;
using Examageddon.Services;
using Examageddon.Tests.Helpers;

namespace Examageddon.Tests;

public class HistoryServiceTests
{
    [Fact]
    public async Task GetHistoryForPersonAsyncReturnsOnlyCompletedSessions()
    {
        await using var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Test" };
        var exam = new Exam { Title = "E1", PassingThreshold = 60, ExamQuestionCount = 1 };
        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();

        ctx.ExamSessions.AddRange(
            new ExamSession { PersonId = person.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 1, IsPassed = true, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct },
            new ExamSession { PersonId = person.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = null, TotalQuestions = 1, CorrectAnswers = 0, IsPassed = null, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct });
        await ctx.SaveChangesAsync();

        var svc = new HistoryService(new HistoryRepository(ctx));
        var history = await svc.GetHistoryForPersonAsync(person.Id);

        Assert.Single(history);
        Assert.True(history[0].IsPassed);
    }

    [Fact]
    public async Task GetHistoryForPersonAsyncReturnsOnlyOwnSessions()
    {
        await using var ctx = TestDbContextFactory.Create();
        var p1 = new Person { Name = "Alice" };
        var p2 = new Person { Name = "Bob" };
        var exam = new Exam { Title = "E1", PassingThreshold = 60, ExamQuestionCount = 1 };
        ctx.Persons.AddRange(p1, p2);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();

        ctx.ExamSessions.AddRange(
            new ExamSession { PersonId = p1.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 1, IsPassed = true, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct },
            new ExamSession { PersonId = p2.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 0, IsPassed = false, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct });
        await ctx.SaveChangesAsync();

        var svc = new HistoryService(new HistoryRepository(ctx));
        var history = await svc.GetHistoryForPersonAsync(p1.Id);

        Assert.Single(history);
        Assert.True(history[0].IsPassed); // Alice's session is IsPassed=true; Bob's is false
    }
}
