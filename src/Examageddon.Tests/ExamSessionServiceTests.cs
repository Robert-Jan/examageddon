using Examageddon.Data;
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Data.Repositories;
using Examageddon.Services;
using Examageddon.Services.Models;
using Examageddon.Tests.Helpers;

namespace Examageddon.Tests;

public class ExamSessionServiceTests
{
    [Fact]
    public async Task CreateSessionAllCreatesAllQuestions()
    {
        var (ctx, person, exam) = await SeedAsync(5);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };

        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(5, session.TotalQuestions);
        Assert.Equal(5, ctx.SessionQuestions.Count(sq => sq.SessionId == session.Id));
    }

    [Fact]
    public async Task CreateSessionLimitedCreatesExamQuestionCount()
    {
        var (ctx, person, exam) = await SeedAsync(5);
        var opts = new SessionOptions { QuestionMode = QuestionMode.Limited, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };

        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(3, session.TotalQuestions); // ExamQuestionCount = 3
        Assert.Equal(3, ctx.SessionQuestions.Count(sq => sq.SessionId == session.Id));
    }

    [Fact]
    public async Task CreateSessionRandomShufflesPositions()
    {
        var (ctx, person, exam) = await SeedAsync(10);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Random, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);

        var s1 = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
        var s2 = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var order1 = ctx.SessionQuestions.Where(sq => sq.SessionId == s1.Id).OrderBy(sq => sq.Position).Select(sq => sq.QuestionId).ToList();
        var order2 = ctx.SessionQuestions.Where(sq => sq.SessionId == s2.Id).OrderBy(sq => sq.Position).Select(sq => sq.QuestionId).ToList();

        Assert.False(order1.SequenceEqual(order2));
    }

    [Fact]
    public async Task SubmitAnswer_SingleAnswer_RecordsCorrectAnswer()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.OrderBy(sq => sq.Position).First(sq => sq.SessionId == session.Id);
        var correctOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correctOption.Id]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.True(answer.IsCorrect);
        Assert.Equal(1.0, answer.Score);
    }

    [Fact]
    public async Task SubmitAnswer_SingleAnswer_RecordsIncorrectAnswer()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.OrderBy(sq => sq.Position).First(sq => sq.SessionId == session.Id);
        var wrongOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && !a.IsCorrect);

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [wrongOption.Id]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.False(answer.IsCorrect);
        Assert.Equal(0.0, answer.Score);
    }

    [Fact]
    public async Task SubmitAnswer_SingleAnswer_RecordsSelectionOption()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.OrderBy(sq => sq.Position).First(sq => sq.SessionId == session.Id);
        var correctOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correctOption.Id]);

        var selections = ctx.SessionAnswerSelections
            .Where(s => ctx.SessionAnswers.Any(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId && sa.Id == s.SessionAnswerId))
            .ToList();
        Assert.Single(selections);
        Assert.Equal(correctOption.Id, selections[0].AnswerOptionId);
    }

    [Fact]
    public async Task SubmitAnswer_MultiAnswer_AllOrNothing_CorrectWhenAllCorrectOptionsSelected()
    {
        var (ctx, person, exam) = await SeedMultiAnswerAsync();
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var correctIds = ctx.AnswerOptions
            .Where(a => a.QuestionId == sq.QuestionId && a.IsCorrect)
            .Select(a => a.Id)
            .ToList();

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, correctIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.True(answer.IsCorrect);
        Assert.Equal(1.0, answer.Score);
    }

    [Fact]
    public async Task SubmitAnswer_MultiAnswer_AllOrNothing_IncorrectWhenOnlyPartialCorrectSelected()
    {
        var (ctx, person, exam) = await SeedMultiAnswerAsync();
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);

        // Pick only ONE of the two correct options
        var oneCorrectId = ctx.AnswerOptions
            .First(a => a.QuestionId == sq.QuestionId && a.IsCorrect)
            .Id;

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [oneCorrectId]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.False(answer.IsCorrect);
        Assert.Equal(0.0, answer.Score);
    }

    [Fact]
    public async Task SubmitAnswer_MultiAnswer_AllOrNothing_IncorrectWhenAllCorrectPlusWrongSelected()
    {
        var (ctx, person, exam) = await SeedMultiAnswerAsync();
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);

        // Select both correct options AND one wrong — should still be incorrect for AllOrNothing
        var allCorrectIds = ctx.AnswerOptions
            .Where(a => a.QuestionId == sq.QuestionId && a.IsCorrect)
            .Select(a => a.Id)
            .ToList();
        var wrongId = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && !a.IsCorrect).Id;
        var selectedIds = allCorrectIds.Append(wrongId).ToList();

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, selectedIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.False(answer.IsCorrect);
        Assert.Equal(0.0, answer.Score);
    }

    [Fact]
    public async Task SubmitAnswer_MultiAnswer_PartialCredit_ScoresProportionally()
    {
        var (ctx, person, exam) = await SeedMultiAnswerAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);

        // Select only one of two correct options, no wrong ones — expect score 0.5
        var oneCorrectId = ctx.AnswerOptions
            .First(a => a.QuestionId == sq.QuestionId && a.IsCorrect)
            .Id;

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [oneCorrectId]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.5, answer.Score);
        Assert.True(answer.IsCorrect); // score > 0
    }

    [Fact]
    public async Task SubmitAnswer_MultiAnswer_PartialCredit_ZeroWhenAllWrong()
    {
        var (ctx, person, exam) = await SeedMultiAnswerAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var wrongId = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && !a.IsCorrect).Id;

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [wrongId]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.0, answer.Score);
        Assert.False(answer.IsCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_Resubmit_ReplacesSelections()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var wrongOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && !a.IsCorrect);
        var correctOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);

        // Submit wrong first, then correct
        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [wrongOption.Id]);
        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correctOption.Id]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.True(answer.IsCorrect);

        // Only one selection should exist
        var selectionCount = ctx.SessionAnswerSelections
            .Count(s => s.SessionAnswerId == answer.Id);
        Assert.Equal(1, selectionCount);
    }

    [Fact]
    public async Task CompleteSession_AllCorrect_CalculatesPass()
    {
        var (ctx, person, exam) = await SeedAsync(3);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        foreach (var sq in ctx.SessionQuestions.Where(sq => sq.SessionId == session.Id))
        {
            var correct = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);
            await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correct.Id]);
        }

        var result = await svc.CompleteSessionAsync(session.Id);

        Assert.True(result.IsPassed); // 100% > 60% passing score
        Assert.Equal(3.0, result.CorrectAnswers);
        var completedSession = await ctx.ExamSessions.FindAsync(session.Id);
        Assert.NotNull(completedSession!.CompletedAt);
    }

    [Fact]
    public async Task CompleteSession_MultiAnswer_PartialCredit_AggregatesScore()
    {
        // Question has 2 correct options; student picks 1 → score 0.5
        var (ctx, person, exam) = await SeedMultiAnswerAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var oneCorrect = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);
        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [oneCorrect.Id]);

        var result = await svc.CompleteSessionAsync(session.Id);

        Assert.Equal(0.5, result.CorrectAnswers);
        Assert.False(result.IsPassed); // 50% < 60%
    }

    [Fact]
    public async Task GetActiveSessionAsyncReturnsSessionWhenIncompleteSessionExists()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        var result = await BuildService(ctx).GetActiveSessionAsync(person.Id);

        Assert.NotNull(result);
        Assert.Equal(person.Id, result.PersonId);
        Assert.Null(result.CompletedAt);
    }

    [Fact]
    public async Task GetActiveSessionAsyncReturnsNullWhenNoSessionExists()
    {
        var (ctx, person, _) = await SeedAsync(2);

        var result = await BuildService(ctx).GetActiveSessionAsync(person.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveSessionAsyncReturnsNullWhenSessionIsCompleted()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
        await svc.CompleteSessionAsync(session.Id);

        var result = await svc.GetActiveSessionAsync(person.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveSessionAsyncReturnsMostRecentWhenMultipleIncompleteSessionsExist()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var svc = BuildService(ctx);
        var first = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
        var second = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        first.StartedAt = DateTime.UtcNow.AddMinutes(-5);
        await ctx.SaveChangesAsync();

        var result = await svc.GetActiveSessionAsync(person.Id);

        Assert.NotNull(result);
        Assert.Equal(second.Id, result.Id);
    }

    [Fact]
    public async Task AbandonSessionAsyncDeletesSessionAndCascadesRelatedRows()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);
        var sessionId = session.Id;

        await BuildService(ctx).AbandonSessionAsync(sessionId, person.Id);

        Assert.Null(await ctx.ExamSessions.FindAsync(sessionId));
        Assert.Empty(ctx.SessionQuestions.Where(sq => sq.SessionId == sessionId));
    }

    [Fact]
    public async Task AbandonSessionAsyncIsNoOpWhenSessionDoesNotExist()
    {
        var (ctx, person, _) = await SeedAsync(2);

        var ex = await Record.ExceptionAsync(() => BuildService(ctx).AbandonSessionAsync(99999, person.Id));

        Assert.Null(ex);
    }

    [Fact]
    public async Task AbandonSessionAsyncIsNoOpWhenSessionBelongsToDifferentPerson()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var otherPerson = new Person { Name = "Other" };
        ctx.Persons.Add(otherPerson);
        await ctx.SaveChangesAsync();

        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        await BuildService(ctx).AbandonSessionAsync(session.Id, otherPerson.Id);

        Assert.NotNull(await ctx.ExamSessions.FindAsync(session.Id));
    }

    [Fact]
    public async Task GetSessionQuestion_ReturnsMultipleChoiceQuestionType()
    {
        var (ctx, person, exam) = await SeedAsync(1);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var model = await svc.GetSessionQuestionAsync(session.Id, 1);

        Assert.NotNull(model);
        Assert.Equal(QuestionType.MultipleChoice, model.QuestionType);
    }

    [Fact]
    public async Task GetSessionQuestion_ReturnsStatementQuestionType()
    {
        var (ctx, person, exam) = await SeedAsync(1);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var question = ctx.Questions.First();
        question.QuestionType = QuestionType.Statement;
        await ctx.SaveChangesAsync();

        var model = await svc.GetSessionQuestionAsync(session.Id, 1);

        Assert.NotNull(model);
        Assert.Equal(QuestionType.Statement, model.QuestionType);
    }

    [Fact]
    public async Task SubmitDragAnswer_AllCorrect_PartialCredit_ScoresOne()
    {
        var (ctx, person, exam) = await SeedDragAndDropAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var slots = ctx.AnswerOptions.Where(a => a.QuestionId == sq.QuestionId).OrderBy(a => a.OrderIndex).ToList();
        var slotIds = slots.ConvertAll(a => a.Id);
        var placedIds = slotIds; // correct: each item in its own slot

        await svc.SubmitDragAnswerAsync(session.Id, sq.QuestionId, slotIds, placedIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(1.0, answer.Score);
        Assert.True(answer.IsCorrect);
    }

    [Fact]
    public async Task SubmitDragAnswer_PartialCorrect_PartialCredit_ScoresPartial()
    {
        var (ctx, person, exam) = await SeedDragAndDropAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var slots = ctx.AnswerOptions.Where(a => a.QuestionId == sq.QuestionId).OrderBy(a => a.OrderIndex).ToList();

        // Slot 0 correct (placedId = slots[0].Id), slot 1 wrong (placed slots[0].Id instead of slots[1].Id)
        var slotIds = slots.ConvertAll(a => a.Id);
        var placedIds = new List<int> { slots[0].Id, slots[0].Id }; // 1 of 2 correct

        await svc.SubmitDragAnswerAsync(session.Id, sq.QuestionId, slotIds, placedIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.5, answer.Score);
        Assert.True(answer.IsCorrect); // score > 0
    }

    [Fact]
    public async Task SubmitDragAnswer_AllWrong_AllOrNothing_ScoresZero()
    {
        var (ctx, person, exam) = await SeedDragAndDropAsync(scoringMode: MultiAnswerScoringMode.AllOrNothing);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var slots = ctx.AnswerOptions.Where(a => a.QuestionId == sq.QuestionId).OrderBy(a => a.OrderIndex).ToList();
        var slotIds = slots.ConvertAll(a => a.Id);
        var placedIds = new List<int> { slots[1].Id, slots[0].Id }; // swapped = all wrong

        await svc.SubmitDragAnswerAsync(session.Id, sq.QuestionId, slotIds, placedIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.0, answer.Score);
        Assert.False(answer.IsCorrect);
    }

    [Fact]
    public async Task SubmitDragAnswer_PartialCorrect_AllOrNothing_ScoresZero()
    {
        var (ctx, person, exam) = await SeedDragAndDropAsync(scoringMode: MultiAnswerScoringMode.AllOrNothing);
        var svc = BuildService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var slots = ctx.AnswerOptions.Where(a => a.QuestionId == sq.QuestionId).OrderBy(a => a.OrderIndex).ToList();
        var slotIds = slots.ConvertAll(a => a.Id);
        var placedIds = new List<int> { slots[0].Id, slots[0].Id }; // 1 of 2 correct

        await svc.SubmitDragAnswerAsync(session.Id, sq.QuestionId, slotIds, placedIds);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.0, answer.Score);
        Assert.False(answer.IsCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_Azure_PartialCredit_WrongPickIgnored()
    {
        // None+PartialCredit with 1 correct + 1 wrong out of 2 correct → (1-1)/2 = 0.0
        // Azure+PartialCredit with same input                           → 1/2 = 0.5
        var (ctx, person, exam) = await SeedAzureAsync();
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var correctId = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect).Id;
        var wrongId = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && !a.IsCorrect).Id;

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correctId, wrongId]);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.Equal(0.5, answer.Score); // 1 correct / 2 correct options; wrong pick not deducted
        Assert.True(answer.IsCorrect);
    }

    [Fact]
    public async Task CompleteSession_Azure_AllCorrect_ScaledScoreIs1000_Passes()
    {
        var (ctx, person, exam) = await SeedAzureAsync();
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
        var correctIds = ctx.AnswerOptions
            .Where(a => a.QuestionId == sq.QuestionId && a.IsCorrect)
            .Select(a => a.Id)
            .ToList();
        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, correctIds);

        var result = await svc.CompleteSessionAsync(session.Id);

        Assert.True(result.IsPassed);
        Assert.Equal(1000, result.ScaledScore);
    }

    [Fact]
    public async Task CompleteSession_Azure_TwoOfThreeCorrect_ScaledScore667_Fails()
    {
        // 2/3 correct → (int)Math.Round(2/3.0 * 1000) = 667 < 700 → fail
        var (ctx, person, exam) = await SeedAzureSingleAsync(3);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sqs = ctx.SessionQuestions.Where(sq => sq.SessionId == session.Id).OrderBy(sq => sq.Position).ToList();

        foreach (var sq in sqs.Take(2))
        {
            var correct = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);
            await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, [correct.Id]);
        }

        var lastSq = sqs[2];
        var wrong = ctx.AnswerOptions.First(a => a.QuestionId == lastSq.QuestionId && !a.IsCorrect);
        await svc.SubmitAnswerAsync(session.Id, lastSq.QuestionId, [wrong.Id]);

        var result = await svc.CompleteSessionAsync(session.Id);

        Assert.False(result.IsPassed);
        Assert.Equal(667, result.ScaledScore);
    }

    [Fact]
    public async Task CreateSession_Azure_SnapshotsScoringPreset()
    {
        var (ctx, person, exam) = await SeedAzureSingleAsync(1);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };

        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(ScoringPreset.AzureCertification, session.ScoringPreset);
    }

    // Seeds an exam with single-correct-answer questions (AllowMultipleAnswers = false)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedAsync(int questionCount = 5)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam { Title = "Demo", PassingThreshold = 60, ExamQuestionCount = 3 };
        for (var i = 0; i < questionCount; i++)
        {
            var q = new Question { Text = $"Q{i}", OrderIndex = i };
            q.AnswerOptions.Add(new AnswerOption { Text = "Correct", IsCorrect = true, OrderIndex = 0 });
            q.AnswerOptions.Add(new AnswerOption { Text = "Wrong", IsCorrect = false, OrderIndex = 1 });
            exam.Questions.Add(q);
        }

        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    // Seeds an exam with one multi-answer question (2 correct, 1 wrong)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedMultiAnswerAsync(
        MultiAnswerScoringMode scoringMode = MultiAnswerScoringMode.AllOrNothing)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam { Title = "Multi Demo", PassingThreshold = 60, ExamQuestionCount = 1 };

        var q = new Question
        {
            Text = "Multi Q",
            OrderIndex = 0,
            AllowMultipleAnswers = true,
            ScoringMode = scoringMode,
        };
        q.AnswerOptions.Add(new AnswerOption { Text = "Correct A", IsCorrect = true, OrderIndex = 0 });
        q.AnswerOptions.Add(new AnswerOption { Text = "Correct B", IsCorrect = true, OrderIndex = 1 });
        q.AnswerOptions.Add(new AnswerOption { Text = "Wrong C", IsCorrect = false, OrderIndex = 2 });
        exam.Questions.Add(q);

        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    // Seeds an exam with one DragAndDrop question (2 pairs: Paris/France, Berlin/Germany)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedDragAndDropAsync(
        MultiAnswerScoringMode scoringMode = MultiAnswerScoringMode.AllOrNothing)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam { Title = "DnD Demo", PassingThreshold = 60, ExamQuestionCount = 1 };

        var q = new Question
        {
            Text = "Match the capitals",
            OrderIndex = 0,
            QuestionType = QuestionType.DragAndDrop,
            AllowMultipleAnswers = true,
            ScoringMode = scoringMode,
        };
        q.AnswerOptions.Add(new AnswerOption { Text = "Capital of France", MatchText = "Paris", IsCorrect = true, OrderIndex = 0 });
        q.AnswerOptions.Add(new AnswerOption { Text = "Capital of Germany", MatchText = "Berlin", IsCorrect = true, OrderIndex = 1 });
        exam.Questions.Add(q);

        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    // Seeds an Azure-preset exam with one multi-answer question (2 correct, 1 wrong, PartialCredit)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedAzureAsync()
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam
        {
            Title = "Azure Demo",
            PassingThreshold = 700,
            ExamQuestionCount = 1,
            ScoringPreset = ScoringPreset.AzureCertification,
        };

        var q = new Question
        {
            Text = "Azure Multi Q",
            OrderIndex = 0,
            AllowMultipleAnswers = true,
            ScoringMode = MultiAnswerScoringMode.PartialCredit,
        };
        q.AnswerOptions.Add(new AnswerOption { Text = "Correct A", IsCorrect = true, OrderIndex = 0 });
        q.AnswerOptions.Add(new AnswerOption { Text = "Correct B", IsCorrect = true, OrderIndex = 1 });
        q.AnswerOptions.Add(new AnswerOption { Text = "Wrong C", IsCorrect = false, OrderIndex = 2 });
        exam.Questions.Add(q);

        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    // Seeds an Azure-preset exam with N single-answer questions
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedAzureSingleAsync(int questionCount)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam
        {
            Title = "Azure Single Demo",
            PassingThreshold = 700,
            ExamQuestionCount = questionCount,
            ScoringPreset = ScoringPreset.AzureCertification,
        };

        for (var i = 0; i < questionCount; i++)
        {
            var q = new Question { Text = $"Q{i}", OrderIndex = i };
            q.AnswerOptions.Add(new AnswerOption { Text = "Correct", IsCorrect = true, OrderIndex = 0 });
            q.AnswerOptions.Add(new AnswerOption { Text = "Wrong", IsCorrect = false, OrderIndex = 1 });
            exam.Questions.Add(q);
        }

        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    private static ExamSessionService BuildService(ExamageddonDbContext ctx)
    {
        return new(new ExamRepository(ctx), new ExamSessionRepository(ctx));
    }
}
