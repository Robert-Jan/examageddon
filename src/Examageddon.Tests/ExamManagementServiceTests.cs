using Examageddon.Data.Entities;
using Examageddon.Data.Repositories;
using Examageddon.Services;
using Examageddon.Tests.Helpers;

namespace Examageddon.Tests;

public class ExamManagementServiceTests
{
    [Fact]
    public async Task CreateExamAsyncPersistsExam()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = new ExamManagementService(new ExamRepository(ctx));
        var exam = MakeExam();

        var created = await svc.CreateExamAsync(exam);

        Assert.NotEqual(0, created.Id);
        Assert.Equal("Test Exam", created.Title);
    }

    [Fact]
    public async Task GetAllExamsAsyncReturnsAll()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Exams.AddRange(MakeExam("A"), MakeExam("B"));
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(new ExamRepository(ctx));

        var exams = await svc.GetAllExamsAsync();

        Assert.Equal(2, exams.Count);
    }

    [Fact]
    public async Task AddQuestionAsyncAttachesToExam()
    {
        await using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(new ExamRepository(ctx));

        var question = new Question
        {
            ExamId = exam.Id,
            Text = "Q1",
            AnswerOptions =
            [
                new AnswerOption { Text = "A", IsCorrect = true, OrderIndex = 0 },
                new AnswerOption { Text = "B", IsCorrect = false, OrderIndex = 1 }
            ],
        };
        var created = await svc.AddQuestionAsync(question);

        Assert.NotEqual(0, created.Id);
        Assert.Equal(2, ctx.AnswerOptions.Count(a => a.QuestionId == created.Id));
    }

    [Fact]
    public async Task DeleteQuestionAsyncRemovesQuestion()
    {
        await using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        var question = new Question { Text = "Q1", ExamId = 0 };
        exam.Questions.Add(question);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(new ExamRepository(ctx));

        await svc.DeleteQuestionAsync(question.Id);

        Assert.Empty(ctx.Questions.Where(q => q.Id == question.Id));
    }

    [Fact]
    public async Task ImageRoundTripStoresAndRetrievesBlob()
    {
        await using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        var question = new Question { Text = "Q1" };
        exam.Questions.Add(question);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(new ExamRepository(ctx));

        byte[] imageData = [1, 2, 3, 4];
        await svc.SaveImageAsync(question.Id, imageData, "image/png");
        var (data, contentType) = await svc.GetImageAsync(question.Id);

        Assert.Equal(imageData, data);
        Assert.Equal("image/png", contentType);
    }

    private static Exam MakeExam(string title = "Test Exam")
    {
        return new()
        {
            Title = title,
            PassingThreshold = 70,
            ExamQuestionCount = 10,
        };
    }
}
