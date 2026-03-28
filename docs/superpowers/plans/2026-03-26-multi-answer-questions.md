# Multi-Answer Questions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional multi-correct-answer support to questions, with all-or-nothing or partial-credit scoring, checkboxes in the session UI, and controls in the manager question form.

**Architecture:** New `MultiAnswerScoringMode` enum and `SessionAnswerSelection` entity replace the single `SelectedAnswerOptionId` on `SessionAnswer` with a collection of selections (unified for both single and multi questions). Service scoring logic branches on `Question.AllowMultipleAnswers` and `Question.ScoringMode`. UI branches in `_AnswerFeedback.cshtml` between immediate-click (single) and checkbox+Submit (multi).

**Tech Stack:** .NET 10, EF Core (SQLite, EnsureCreated), Razor Pages, HTMX, XUnit

---

## File Map

| File | Action | What changes |
|---|---|---|
| `src/Examageddon.Data/Enums/MultiAnswerScoringMode.cs` | **Create** | New enum |
| `src/Examageddon.Data/Entities/SessionAnswerSelection.cs` | **Create** | New entity |
| `src/Examageddon.Data/Entities/Question.cs` | Modify | Add `AllowMultipleAnswers`, `ScoringMode` |
| `src/Examageddon.Data/Entities/SessionAnswer.cs` | Modify | Remove `SelectedAnswerOptionId`/nav, add `Score` + `Selections` |
| `src/Examageddon.Data/Entities/ExamSession.cs` | Modify | `CorrectAnswers` int → double |
| `src/Examageddon.Data/ExamageddonDbContext.cs` | Modify | Add `DbSet<SessionAnswerSelection>` |
| `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs` | Modify | Replace `GetAnswerOptionAsync` with `GetAnswerOptionsAsync` |
| `src/Examageddon.Data/Repositories/ExamSessionRepository.cs` | Modify | Update `GetAnswerAsync` (Include Selections), `GetFullSessionAsync`, replace option method |
| `src/Examageddon.Services/Models/SessionQuestionModel.cs` | Modify | `ExistingAnswerOptionId` → `SelectedAnswerOptionIds` set, add `AllowMultipleAnswers` |
| `src/Examageddon.Services/Models/QuestionResultItem.cs` | Modify | `SelectedOption` → `SelectedOptions` list |
| `src/Examageddon.Services/Models/SessionResultModel.cs` | Modify | `CorrectAnswers` int → double |
| `src/Examageddon.Services/ExamSessionService.cs` | Modify | `SubmitAnswerAsync` new signature + logic; update `GetSessionQuestionAsync`, `CompleteSessionAsync`, `GetResultAsync` |
| `src/Examageddon.Tests/ExamSessionServiceTests.cs` | Modify | Update call sites, add multi-answer tests |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` | Modify | Add `AllowMultipleAnswers` checkbox, scoring mode radios, change answer radios to checkboxes |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs` | Modify | New bound properties, updated OnGet/OnPost |
| `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml` | Modify | Branch on `AllowMultipleAnswers`; add checkbox+submit path |
| `src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs` | Modify | `OnPostAnswerAsync` — `int answerId` → `List<int> answerIds` |
| `src/Examageddon.Web/Pages/Sessions/Result.cshtml` | Modify | `SelectedOption` → `SelectedOptions`, show all correct options when wrong |
| `src/Examageddon.Web/Pages/Sessions/Review.cshtml` | Modify | `SelectedOption` → `SelectedOptions` |

---

## Task 1: Data entities

**Files:**
- Create: `src/Examageddon.Data/Enums/MultiAnswerScoringMode.cs`
- Create: `src/Examageddon.Data/Entities/SessionAnswerSelection.cs`
- Modify: `src/Examageddon.Data/Entities/Question.cs`
- Modify: `src/Examageddon.Data/Entities/SessionAnswer.cs`
- Modify: `src/Examageddon.Data/Entities/ExamSession.cs`
- Modify: `src/Examageddon.Data/ExamageddonDbContext.cs`

- [ ] **Step 1: Create `MultiAnswerScoringMode` enum**

Create `src/Examageddon.Data/Enums/MultiAnswerScoringMode.cs`:

```csharp
namespace Examageddon.Data.Enums;

public enum MultiAnswerScoringMode
{
    AllOrNothing = 0,
    PartialCredit = 1,
}
```

- [ ] **Step 2: Create `SessionAnswerSelection` entity**

Create `src/Examageddon.Data/Entities/SessionAnswerSelection.cs`:

```csharp
namespace Examageddon.Data.Entities;

public class SessionAnswerSelection
{
    public int Id { get; set; }

    public int SessionAnswerId { get; set; }

    public SessionAnswer SessionAnswer { get; set; } = null!;

    public int AnswerOptionId { get; set; }

    public AnswerOption AnswerOption { get; set; } = null!;
}
```

- [ ] **Step 3: Update `Question.cs`**

Add two properties at the bottom of the class (after `OrderIndex`):

```csharp
public bool AllowMultipleAnswers { get; set; }

public MultiAnswerScoringMode ScoringMode { get; set; }
```

The file needs `using Examageddon.Data.Enums;` — it already has that import.

Full updated file:

```csharp
using Examageddon.Data.Enums;

namespace Examageddon.Data.Entities;

public class Question
{
    public int Id { get; set; }

    public int ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

    public byte[]? ImageData { get; set; }

    public string? ImageContentType { get; set; }

    public int OrderIndex { get; set; }

    public bool AllowMultipleAnswers { get; set; }

    public MultiAnswerScoringMode ScoringMode { get; set; }

    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];
}
```

- [ ] **Step 4: Update `SessionAnswer.cs`**

Remove `SelectedAnswerOptionId` and `SelectedAnswerOption`. Add `Score` (double) and `Selections` collection.

Full updated file:

```csharp
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
```

- [ ] **Step 5: Update `ExamSession.cs`**

Change `CorrectAnswers` from `int` to `double`:

```csharp
public double CorrectAnswers { get; set; }
```

- [ ] **Step 6: Update `ExamageddonDbContext.cs`**

Add `DbSet<SessionAnswerSelection>` after the `SessionAnswers` DbSet:

```csharp
public DbSet<SessionAnswerSelection> SessionAnswerSelections => Set<SessionAnswerSelection>();
```

- [ ] **Step 7: Build the Data project to check for compile errors**

Run: `dotnet build src/Examageddon.Data/Examageddon.Data.csproj`

Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/Examageddon.Data/Enums/MultiAnswerScoringMode.cs
git add src/Examageddon.Data/Entities/SessionAnswerSelection.cs
git add src/Examageddon.Data/Entities/Question.cs
git add src/Examageddon.Data/Entities/SessionAnswer.cs
git add src/Examageddon.Data/Entities/ExamSession.cs
git add src/Examageddon.Data/ExamageddonDbContext.cs
git commit -m "feat: add multi-answer data model (SessionAnswerSelection, ScoringMode)"
```

---

## Task 2: Repository layer

**Files:**
- Modify: `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs`
- Modify: `src/Examageddon.Data/Repositories/ExamSessionRepository.cs`

- [ ] **Step 1: Update `IExamSessionRepository`**

Replace `Task<AnswerOption?> GetAnswerOptionAsync(int answerOptionId);` with:

```csharp
Task<List<AnswerOption>> GetAnswerOptionsAsync(IEnumerable<int> answerOptionIds);
```

Full updated interface:

```csharp
using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IExamSessionRepository
{
    Task<ExamSession> CreateAsync(ExamSession session, IReadOnlyList<SessionQuestion> sessionQuestions);

    Task<ExamSession?> GetByIdAsync(int sessionId);

    Task<SessionQuestion?> GetSessionQuestionAsync(int sessionId, int position);

    Task<SessionAnswer?> GetAnswerAsync(int sessionId, int questionId);

    Task<List<AnswerOption>> GetAnswerOptionsAsync(IEnumerable<int> answerOptionIds);

    Task AddAnswerAsync(SessionAnswer answer);

    Task<ExamSession?> GetWithExamAndAnswersAsync(int sessionId);

    Task<ExamSession?> GetFullSessionAsync(int sessionId);

    Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId);

    Task SaveChangesAsync();

    Task<ExamSession?> GetActiveByPersonAsync(int personId);

    Task<int?> GetFirstUnansweredPositionAsync(int sessionId);

    Task DeleteAsync(int sessionId);
}
```

- [ ] **Step 2: Update `ExamSessionRepository`**

Three changes:

1. `GetAnswerAsync` — add `.Include(sa => sa.Selections)` so selections are loaded and tracked for replacement.

2. Replace `GetAnswerOptionAsync` with `GetAnswerOptionsAsync`.

3. `GetFullSessionAsync` — remove the old `.ThenInclude(sa => sa.SelectedAnswerOption)` (property no longer exists), replace with `.ThenInclude(sa => sa.Selections).ThenInclude(sel => sel.AnswerOption)`.

Full updated file:

```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data.Repositories;

internal sealed class ExamSessionRepository(ExamageddonDbContext db) : IExamSessionRepository
{
    public async Task<ExamSession> CreateAsync(ExamSession session, IReadOnlyList<SessionQuestion> sessionQuestions)
    {
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();

        foreach (var sq in sessionQuestions)
        {
            sq.SessionId = session.Id;
        }

        db.SessionQuestions.AddRange(sessionQuestions);
        await db.SaveChangesAsync();

        return session;
    }

    public Task<ExamSession?> GetByIdAsync(int sessionId)
    {
        return db.ExamSessions.FindAsync(sessionId).AsTask();
    }

    public Task<SessionQuestion?> GetSessionQuestionAsync(int sessionId, int position)
    {
        return db.SessionQuestions
            .Include(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(sq => sq.SessionId == sessionId && sq.Position == position);
    }

    public Task<SessionAnswer?> GetAnswerAsync(int sessionId, int questionId)
    {
        return db.SessionAnswers
            .Include(sa => sa.Selections)
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.QuestionId == questionId);
    }

    public Task<List<AnswerOption>> GetAnswerOptionsAsync(IEnumerable<int> answerOptionIds)
    {
        var ids = answerOptionIds.ToList();
        return db.AnswerOptions.Where(a => ids.Contains(a.Id)).ToListAsync();
    }

    public async Task AddAnswerAsync(SessionAnswer answer)
    {
        db.SessionAnswers.Add(answer);
        await db.SaveChangesAsync();
    }

    public Task<ExamSession?> GetWithExamAndAnswersAsync(int sessionId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionAnswers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public Task<ExamSession?> GetFullSessionAsync(int sessionId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionQuestions).ThenInclude(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Include(s => s.SessionAnswers).ThenInclude(sa => sa.Selections).ThenInclude(sel => sel.AnswerOption)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId)
    {
        return db.SessionQuestions
            .Include(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Where(sq => sq.SessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToListAsync();
    }

    public Task SaveChangesAsync()
    {
        return db.SaveChangesAsync();
    }

    public Task<ExamSession?> GetActiveByPersonAsync(int personId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Where(s => s.PersonId == personId && s.CompletedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetFirstUnansweredPositionAsync(int sessionId)
    {
        return db.SessionQuestions
            .Where(sq => sq.SessionId == sessionId &&
                         !db.SessionAnswers.Any(sa => sa.SessionId == sessionId && sa.QuestionId == sq.QuestionId))
            .OrderBy(sq => sq.Position)
            .Select(sq => (int?)sq.Position)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteAsync(int sessionId)
    {
        var session = await db.ExamSessions.FindAsync(sessionId);
        if (session is not null)
        {
            db.ExamSessions.Remove(session);
            await db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 3: Build the Data project**

Run: `dotnet build src/Examageddon.Data/Examageddon.Data.csproj`

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Data/Interfaces/IExamSessionRepository.cs
git add src/Examageddon.Data/Repositories/ExamSessionRepository.cs
git commit -m "feat: update repository for multi-answer selections"
```

---

## Task 3: Service models

**Files:**
- Modify: `src/Examageddon.Services/Models/SessionQuestionModel.cs`
- Modify: `src/Examageddon.Services/Models/QuestionResultItem.cs`
- Modify: `src/Examageddon.Services/Models/SessionResultModel.cs`

- [ ] **Step 1: Update `SessionQuestionModel.cs`**

Replace `int? ExistingAnswerOptionId` and the `IsAnswered` computed property with `SelectedAnswerOptionIds` (an `IReadOnlySet<int>`). Add `AllowMultipleAnswers`.

Full updated file:

```csharp
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

    public bool IsAnswered => SelectedAnswerOptionIds.Count > 0;

    public bool AllowMultipleAnswers { get; set; }

    public bool IsFromReview { get; set; }
}
```

- [ ] **Step 2: Update `QuestionResultItem.cs`**

Replace `AnswerOption? SelectedOption` with `IReadOnlyList<AnswerOption> SelectedOptions`.

Full updated file:

```csharp
using Examageddon.Data.Entities;

namespace Examageddon.Services.Models;

public class QuestionResultItem
{
    public Question Question { get; set; } = null!;

    public IReadOnlyList<AnswerOption> SelectedOptions { get; set; } = [];

    public bool IsCorrect { get; set; }

    public bool IsAnswered { get; set; }
}
```

- [ ] **Step 3: Update `SessionResultModel.cs`**

Change `CorrectAnswers` from `int` to `double`. The `ScorePercent` computed property works unchanged.

Full updated file:

```csharp
namespace Examageddon.Services.Models;

public class SessionResultModel
{
    public int SessionId { get; set; }

    public string ExamTitle { get; set; } = string.Empty;

    public int TotalQuestions { get; set; }

    public double CorrectAnswers { get; set; }

    public int PassingScorePercent { get; set; }

    public bool IsPassed { get; set; }

    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round(CorrectAnswers / TotalQuestions * 100, 1);

    public List<QuestionResultItem> QuestionResults { get; set; } = [];
}
```

- [ ] **Step 4: Build the Services project**

Run: `dotnet build src/Examageddon.Services/Examageddon.Services.csproj`

Expected: compile errors in `ExamSessionService.cs` because it still references `ExistingAnswerOptionId`, `SelectedAnswerOption`, etc. — that is expected here; we fix them in Task 4.

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Services/Models/SessionQuestionModel.cs
git add src/Examageddon.Services/Models/QuestionResultItem.cs
git add src/Examageddon.Services/Models/SessionResultModel.cs
git commit -m "feat: update service models for multi-answer"
```

---

## Task 4: Service logic

**Files:**
- Modify: `src/Examageddon.Services/ExamSessionService.cs`

- [ ] **Step 1: Write the updated `ExamSessionService.cs`**

Four methods change: `GetSessionQuestionAsync`, `SubmitAnswerAsync`, `CompleteSessionAsync`, `GetResultAsync`.

Full updated file:

```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Data.Interfaces;
using Examageddon.Services.Models;

namespace Examageddon.Services;

public class ExamSessionService(IExamRepository examRepo, IExamSessionRepository sessionRepo)
{
    public async Task<ExamSession> CreateSessionAsync(int personId, int examId, SessionOptions opts)
    {
        var exam = await examRepo.GetByIdWithQuestionsAsync(examId)
            ?? throw new InvalidOperationException($"Exam {examId} not found.");

        var questions = exam.Questions.OrderBy(q => q.OrderIndex).ToList();
        if (opts.QuestionMode == QuestionMode.Limited)
        {
            questions = [.. questions.Take(exam.ExamQuestionCount)];
        }

        if (opts.OrderMode == OrderMode.Random)
        {
            questions = [.. questions.OrderBy(_ => Guid.NewGuid())];
        }

        var session = new ExamSession
        {
            PersonId = personId,
            ExamId = examId,
            StartedAt = DateTime.UtcNow,
            QuestionMode = opts.QuestionMode,
            OrderMode = opts.OrderMode,
            FeedbackMode = opts.FeedbackMode,
            TotalQuestions = questions.Count,
        };

        var sessionQuestions = questions
            .Select((q, i) => new SessionQuestion { QuestionId = q.Id, Position = i + 1 })
            .ToList();

        return await sessionRepo.CreateAsync(session, sessionQuestions);
    }

    public async Task<SessionQuestionModel?> GetSessionQuestionAsync(int sessionId, int position, bool fromReview = false)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            return null;
        }

        var sq = await sessionRepo.GetSessionQuestionAsync(sessionId, position);
        if (sq is null)
        {
            return null;
        }

        var answer = await sessionRepo.GetAnswerAsync(sessionId, sq.QuestionId);
        var selectedIds = answer?.Selections.Select(s => s.AnswerOptionId).ToHashSet() ?? [];

        return new SessionQuestionModel
        {
            SessionId = sessionId,
            Position = position,
            TotalQuestions = session.TotalQuestions,
            FeedbackMode = session.FeedbackMode,
            Question = sq.Question,
            AnswerOptions = [.. sq.Question.AnswerOptions.OrderBy(a => a.OrderIndex)],
            SelectedAnswerOptionIds = selectedIds,
            AllowMultipleAnswers = sq.Question.AllowMultipleAnswers,
            IsFromReview = fromReview,
        };
    }

    public async Task SubmitAnswerAsync(int sessionId, int questionId, IReadOnlyList<int> answerOptionIds)
    {
        var question = await examRepo.GetQuestionAsync(questionId);
        if (question is null)
        {
            return;
        }

        var selectedIds = answerOptionIds.ToHashSet();
        var correctIds = question.AnswerOptions.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

        double score;
        bool isCorrect;

        if (!question.AllowMultipleAnswers || question.ScoringMode == MultiAnswerScoringMode.AllOrNothing)
        {
            isCorrect = selectedIds.SetEquals(correctIds);
            score = isCorrect ? 1.0 : 0.0;
        }
        else
        {
            var correctSelected = selectedIds.Count(id => correctIds.Contains(id));
            var wrongSelected = selectedIds.Count(id => !correctIds.Contains(id));
            score = correctIds.Count == 0
                ? 0.0
                : Math.Max(0.0, (correctSelected - wrongSelected) / (double)correctIds.Count);
            isCorrect = score > 0;
        }

        var selections = answerOptionIds
            .Select(id => new SessionAnswerSelection { AnswerOptionId = id })
            .ToList();

        var existing = await sessionRepo.GetAnswerAsync(sessionId, questionId);

        if (existing is not null)
        {
            existing.Score = score;
            existing.IsCorrect = isCorrect;
            existing.AnsweredAt = DateTime.UtcNow;
            existing.Selections.Clear();
            foreach (var sel in selections)
            {
                existing.Selections.Add(sel);
            }
            await sessionRepo.SaveChangesAsync();
        }
        else
        {
            await sessionRepo.AddAnswerAsync(new SessionAnswer
            {
                SessionId = sessionId,
                QuestionId = questionId,
                Score = score,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow,
                Selections = selections,
            });
        }
    }

    public async Task<SessionResultModel> CompleteSessionAsync(int sessionId)
    {
        var session = await sessionRepo.GetWithExamAndAnswersAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var totalScore = session.SessionAnswers.Sum(a => a.Score);
        var scorePercent = session.TotalQuestions == 0 ? 0 : totalScore / session.TotalQuestions * 100;

        session.CorrectAnswers = totalScore;
        session.IsPassed = scorePercent >= session.Exam.PassingScorePercent;
        session.CompletedAt = DateTime.UtcNow;
        await sessionRepo.SaveChangesAsync();

        return await GetResultAsync(sessionId);
    }

    public async Task<SessionResultModel> GetResultAsync(int sessionId)
    {
        var session = await sessionRepo.GetFullSessionAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var items = session.SessionQuestions.OrderBy(sq => sq.Position).Select(sq =>
        {
            var answer = session.SessionAnswers.FirstOrDefault(sa => sa.QuestionId == sq.QuestionId);
            return new QuestionResultItem
            {
                Question = sq.Question,
                SelectedOptions = answer?.Selections.Select(s => s.AnswerOption).ToList() ?? [],
                IsCorrect = answer?.IsCorrect ?? false,
                IsAnswered = answer is not null,
            };
        }).ToList();

        return new SessionResultModel
        {
            SessionId = sessionId,
            ExamTitle = session.Exam.Title,
            TotalQuestions = session.TotalQuestions,
            CorrectAnswers = session.CorrectAnswers,
            PassingScorePercent = session.Exam.PassingScorePercent,
            IsPassed = session.IsPassed ?? false,
            QuestionResults = items,
        };
    }

    public Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId)
    {
        return sessionRepo.GetReviewQuestionsAsync(sessionId);
    }

    public Task<ExamSession?> GetActiveSessionAsync(int personId)
    {
        return sessionRepo.GetActiveByPersonAsync(personId);
    }

    public async Task<int> GetResumePositionAsync(int sessionId)
    {
        return await sessionRepo.GetFirstUnansweredPositionAsync(sessionId) ?? 1;
    }

    public async Task AbandonSessionAsync(int sessionId, int personId)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null || session.PersonId != personId)
        {
            return;
        }

        await sessionRepo.DeleteAsync(sessionId);
    }
}
```

- [ ] **Step 2: Build the Services project**

Run: `dotnet build src/Examageddon.Services/Examageddon.Services.csproj`

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Services/ExamSessionService.cs
git commit -m "feat: update ExamSessionService for multi-answer scoring"
```

---

## Task 5: Tests

**Files:**
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 1: Write failing tests**

The existing tests will fail to compile because `SubmitAnswerAsync` now takes `IReadOnlyList<int>` and `result.CorrectAnswers` is now `double`. Write the updated file with both fixes and new multi-answer tests.

Full updated file:

```csharp
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

    // Seeds an exam with single-correct-answer questions (AllowMultipleAnswers = false)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedAsync(int questionCount = 5)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam { Title = "Demo", PassingScorePercent = 60, ExamQuestionCount = 3 };
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
        var exam = new Exam { Title = "Multi Demo", PassingScorePercent = 60, ExamQuestionCount = 1 };

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

    private static ExamSessionService BuildService(ExamageddonDbContext ctx)
    {
        return new(new ExamRepository(ctx), new ExamSessionRepository(ctx));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail (compilation)**

Run: `dotnet test src/Examageddon.Tests/`

Expected: Tests should compile. If `SessionAnswerSelections` DbSet is missing from the test context or something similar fails, fix it before proceeding.

- [ ] **Step 3: Run the tests to verify they pass**

Run: `dotnet test src/Examageddon.Tests/`

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "test: update and extend ExamSessionServiceTests for multi-answer"
```

---

## Task 6: Manager UI

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs`

- [ ] **Step 1: Update `QuestionForm.cshtml.cs`**

Full updated file:

```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class QuestionFormModel(ExamManagementService examService) : PageModel
{
    [BindProperty]
    public int ExamId { get; set; }

    [BindProperty]
    public string QuestionText { get; set; } = string.Empty;

    [BindProperty]
    public bool AllowMultipleAnswers { get; set; }

    [BindProperty]
    public MultiAnswerScoringMode ScoringMode { get; set; }

    [BindProperty]
    public List<string> OptionTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];

    [BindProperty]
    public List<int> CorrectOptionIndices { get; set; } = [];

    [BindProperty]
    public IFormFile? Image { get; set; }

    public int? EditQuestionId { get; set; }

    public Question? ExistingQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int? questionId)
    {
        ExamId = id;
        if (questionId.HasValue && questionId.Value != 0)
        {
            EditQuestionId = questionId;
            ExistingQuestion = await examService.GetQuestionAsync(questionId.Value);
            if (ExistingQuestion is null)
            {
                return NotFound();
            }

            QuestionText = ExistingQuestion.Text;
            AllowMultipleAnswers = ExistingQuestion.AllowMultipleAnswers;
            ScoringMode = ExistingQuestion.ScoringMode;
            OptionTexts = [.. ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.Text)];
            CorrectOptionIndices = ExistingQuestion.AnswerOptions
                .OrderBy(a => a.OrderIndex)
                .Select((a, i) => (a, i))
                .Where(x => x.a.IsCorrect)
                .Select(x => x.i)
                .ToList();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, int? questionId)
    {
        var options = OptionTexts
            .Select((text, idx) => new AnswerOption
            {
                Text = text,
                IsCorrect = CorrectOptionIndices.Contains(idx),
                OrderIndex = idx,
            })
            .Where(a => !string.IsNullOrWhiteSpace(a.Text))
            .ToList();

        byte[]? imageData = null;
        string? imageContentType = null;
        if (Image is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await Image.CopyToAsync(ms);
            imageData = ms.ToArray();
            imageContentType = Image.ContentType;
        }

        if (questionId.HasValue && questionId.Value != 0)
        {
            var existing = await examService.GetQuestionAsync(questionId.Value);
            if (existing is null)
            {
                return NotFound();
            }

            existing.Text = QuestionText;
            existing.AllowMultipleAnswers = AllowMultipleAnswers;
            existing.ScoringMode = ScoringMode;
            existing.AnswerOptions = options;
            if (imageData is not null)
            {
                existing.ImageData = imageData;
                existing.ImageContentType = imageContentType;
            }

            await examService.UpdateQuestionAsync(existing);
        }
        else
        {
            var qs = await examService.GetQuestionsAsync(id);
            var question = new Question
            {
                ExamId = id,
                Text = QuestionText,
                QuestionType = QuestionType.MultipleChoice,
                AllowMultipleAnswers = AllowMultipleAnswers,
                ScoringMode = ScoringMode,
                OrderIndex = qs.Count,
                ImageData = imageData,
                ImageContentType = imageContentType,
                AnswerOptions = options,
            };
            await examService.AddQuestionAsync(question);
        }

        return RedirectToPage("/Manage/Questions", new { id });
    }
}
```

- [ ] **Step 2: Update `QuestionForm.cshtml`**

Full updated file:

```cshtml
@page "/manage/exams/{id:int}/questions/{questionId:int?}/edit"
@model Examageddon.Web.Pages.Manage.QuestionFormModel
@using Examageddon.Data.Enums
@{ ViewData["Title"] = Model.EditQuestionId.HasValue ? "Edit Question" : "Add Question"; }

<h1 class="text-3xl font-black mb-8">@ViewData["Title"]</h1>

<form method="post" enctype="multipart/form-data" class="space-y-6 max-w-2xl">
    <input type="hidden" asp-for="ExamId" />

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Question Text *</label>
        <textarea asp-for="QuestionText" rows="3" required
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire"></textarea>
    </div>

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Image (optional)</label>
        @if (Model.ExistingQuestion?.ImageData != null)
        {
            <img src="/images/question/@Model.ExistingQuestion.Id" class="rounded-lg max-h-40 object-contain mb-3 border border-arcade-border" id="img-preview" />
        }
        else
        {
            <img id="img-preview" class="hidden rounded-lg max-h-40 object-contain mb-3 border border-arcade-border" />
        }
        <input type="file" asp-for="Image" accept="image/*"
            class="text-arcade-muted text-sm"
            onchange="previewImage(this)" />
    </div>

    <div class="flex items-center gap-3">
        <input type="checkbox" asp-for="AllowMultipleAnswers" id="allowMultiple"
               class="accent-yellow-500 w-4 h-4"
               onchange="toggleScoringMode(this)" />
        <label for="allowMultiple" class="text-sm font-bold text-arcade-muted cursor-pointer">
            Allow multiple correct answers
        </label>
    </div>

    <div id="scoring-mode-section" class="@(Model.AllowMultipleAnswers ? "" : "hidden") pl-7 space-y-2">
        <p class="text-sm font-bold text-arcade-muted mb-2">Scoring Mode</p>
        <label class="flex items-center gap-2 cursor-pointer">
            <input type="radio" name="ScoringMode" value="@((int)MultiAnswerScoringMode.AllOrNothing)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing ? "checked" : "")
                   class="accent-yellow-500" />
            <span class="text-sm text-arcade-text">All or nothing — must select all correct options</span>
        </label>
        <label class="flex items-center gap-2 cursor-pointer">
            <input type="radio" name="ScoringMode" value="@((int)MultiAnswerScoringMode.PartialCredit)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.PartialCredit ? "checked" : "")
                   class="accent-yellow-500" />
            <span class="text-sm text-arcade-text">Partial credit — score per correct option selected</span>
        </label>
    </div>

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-3">Answer Options (mark the correct one(s))</label>
        <div class="space-y-3">
            @for (int i = 0; i < Model.OptionTexts.Count; i++)
            {
                <div class="flex items-center gap-3">
                    <input type="checkbox" name="CorrectOptionIndices" value="@i"
                           @(Model.CorrectOptionIndices.Contains(i) ? "checked" : "")
                           class="accent-yellow-500 w-4 h-4 flex-shrink-0" />
                    <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
                           placeholder="Option @(i + 1)"
                           class="flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
                </div>
            }
        </div>
    </div>

    <div class="flex gap-4">
        <button type="submit"
            class="px-8 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
            @(Model.EditQuestionId.HasValue ? "Save Question" : "Add Question")
        </button>
        <a href="/manage/exams/@Model.ExamId/questions"
           class="px-8 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
            Cancel
        </a>
    </div>
</form>

@section Scripts {
<script>
function previewImage(input) {
    const preview = document.getElementById('img-preview');
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = e => { preview.src = e.target.result; preview.classList.remove('hidden'); };
        reader.readAsDataURL(input.files[0]);
    }
}

function toggleScoringMode(checkbox) {
    document.getElementById('scoring-mode-section').classList.toggle('hidden', !checkbox.checked);
}
</script>
}
```

- [ ] **Step 3: Build the Web project**

Run: `dotnet build src/Examageddon.Web/Examageddon.Web.csproj`

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs
git commit -m "feat: add multi-answer controls to manager question form"
```

---

## Task 7: Session answer UI

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`
- Modify: `src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs`

- [ ] **Step 1: Update `Question.cshtml.cs`**

Change `OnPostAnswerAsync` to accept `List<int> answerIds` instead of `int answerId`, and pass it to `SubmitAnswerAsync`.

Full updated file:

```csharp
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class QuestionModel(ExamSessionService sessionService) : PageModel
{
    public SessionQuestionModel? SessionQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int sessionId, int n, bool fromReview = false)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n, fromReview);
        if (SessionQuestion is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAnswerAsync(int sessionId, int n, int questionId, List<int> answerIds)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        await sessionService.SubmitAnswerAsync(sessionId, questionId, answerIds);
        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n);
        if (SessionQuestion is null)
        {
            return NotFound();
        }

        return Partial("_AnswerFeedback", SessionQuestion);
    }
}
```

- [ ] **Step 2: Update `_AnswerFeedback.cshtml`**

Full updated file. Single-answer questions keep immediate HTMX click behavior (parameter renamed from `answerId` to `answerIds`). Multi-answer questions show checkboxes + Submit button when unanswered, locked display when answered.

```cshtml
@model Examageddon.Services.Models.SessionQuestionModel
@using Examageddon.Data.Enums

@if (!Model.AllowMultipleAnswers)
{
    <div class="space-y-3">
        @foreach (var option in Model.AnswerOptions)
        {
            var isSelected = Model.SelectedAnswerOptionIds.Contains(option.Id);
            var isAnswered = Model.IsAnswered && Model.FeedbackMode == FeedbackMode.Direct;

            string borderClass, bgClass, textClass;

            if (isAnswered)
            {
                if (option.IsCorrect)
                { borderClass = "border-green-500"; bgClass = "bg-green-500/10"; textClass = "text-green-400"; }
                else if (isSelected)
                { borderClass = "border-red-500"; bgClass = "bg-red-500/10"; textClass = "text-red-400"; }
                else
                { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-muted opacity-50"; }
            }
            else if (isSelected)
            { borderClass = "border-fire"; bgClass = "bg-fire/10"; textClass = "text-fire"; }
            else
            { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-text"; }

            @if (isAnswered)
            {
                <div class="border @borderClass @bgClass rounded-xl p-4 @textClass font-medium flex items-center gap-3">
                    @if (option.IsCorrect) { <span>✓</span> }
                    else if (isSelected) { <span>✗</span> }
                    else { <span class="w-4"></span> }
                    @option.Text
                </div>
            }
            else
            {
                <button type="button"
                    hx-post="/sessions/@Model.SessionId/question/@Model.Position?handler=Answer&questionId=@Model.Question.Id&answerIds=@option.Id"
                    hx-target="#answer-container"
                    hx-swap="innerHTML"
                    class="w-full text-left border @borderClass @bgClass rounded-xl p-4 @textClass font-medium hover:border-fire hover:text-fire transition">
                    @option.Text
                </button>
            }
        }
    </div>
}
else if (!Model.IsAnswered)
{
    <form id="multi-answer-form" class="space-y-3">
        @foreach (var option in Model.AnswerOptions)
        {
            <label class="flex items-center gap-3 border border-arcade-border rounded-xl p-4 text-arcade-text font-medium cursor-pointer hover:border-fire hover:text-fire transition">
                <input type="checkbox" name="answerIds" value="@option.Id" class="accent-yellow-500 w-4 h-4 flex-shrink-0" />
                @option.Text
            </label>
        }
    </form>
    <button type="button"
        hx-post="/sessions/@Model.SessionId/question/@Model.Position?handler=Answer&questionId=@Model.Question.Id"
        hx-include="#multi-answer-form"
        hx-target="#answer-container"
        hx-swap="innerHTML"
        class="mt-4 w-full px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
        Submit Answer
    </button>
}
else
{
    var showFeedback = Model.FeedbackMode == FeedbackMode.Direct;
    <div class="space-y-3">
        @foreach (var option in Model.AnswerOptions)
        {
            var isSelected = Model.SelectedAnswerOptionIds.Contains(option.Id);

            string borderClass, bgClass, textClass;

            if (showFeedback)
            {
                if (option.IsCorrect)
                { borderClass = "border-green-500"; bgClass = "bg-green-500/10"; textClass = "text-green-400"; }
                else if (isSelected)
                { borderClass = "border-red-500"; bgClass = "bg-red-500/10"; textClass = "text-red-400"; }
                else
                { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-muted opacity-50"; }
            }
            else if (isSelected)
            { borderClass = "border-fire"; bgClass = "bg-fire/10"; textClass = "text-fire"; }
            else
            { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-text"; }

            <label class="flex items-center gap-3 border @borderClass @bgClass rounded-xl p-4 @textClass font-medium">
                @if (showFeedback)
                {
                    if (option.IsCorrect) { <span>✓</span> }
                    else if (isSelected) { <span>✗</span> }
                    else { <span class="w-4"></span> }
                }
                <input type="checkbox" disabled @(isSelected ? "checked" : "") class="accent-yellow-500 w-4 h-4 flex-shrink-0" />
                @option.Text
            </label>
        }
    </div>
}

@if (Model.IsAnswered && Model.FeedbackMode == FeedbackMode.Direct)
{
    <div class="mt-6">
        @if (Model.Position < Model.TotalQuestions)
        {
            <a href="/sessions/@Model.SessionId/question/@(Model.Position + 1)"
               class="inline-block w-full text-center px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
                Next →
            </a>
        }
        else
        {
            <form method="post" action="/sessions/@Model.SessionId/complete">
                <button type="submit"
                    class="w-full px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
                    Finish 🏁
                </button>
            </form>
        }
    </div>
}
```

- [ ] **Step 3: Build the Web project**

Run: `dotnet build src/Examageddon.Web/Examageddon.Web.csproj`

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml
git add src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs
git commit -m "feat: add checkbox+submit multi-answer session UI"
```

---

## Task 8: Result and Review pages

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/Result.cshtml`
- Modify: `src/Examageddon.Web/Pages/Sessions/Review.cshtml`

- [ ] **Step 1: Update `Result.cshtml`**

Replace `item.SelectedOption` (single) with `item.SelectedOptions` (list). When incorrect, show all correct options (supports multi-correct questions).

Full updated file:

```cshtml
@page "/sessions/{sessionId:int}/result"
@model Examageddon.Web.Pages.Sessions.ResultModel
@{ ViewData["Title"] = "Result"; var r = Model.Result; }

<div class="text-center mb-10">
    @if (r.IsPassed)
    {
        <div class="text-6xl mb-4">🏆</div>
        <h1 class="text-4xl font-black text-green-400 mb-2">You Passed!</h1>
    }
    else
    {
        <div class="text-6xl mb-4">💀</div>
        <h1 class="text-4xl font-black text-red-400 mb-2">Not Quite...</h1>
    }
    <p class="text-arcade-muted">@r.ExamTitle</p>
</div>

<div class="bg-arcade-card border border-arcade-border rounded-xl p-6 mb-8">
    <div class="grid grid-cols-3 gap-4 text-center">
        <div>
            <div class="text-3xl font-black @(r.IsPassed ? "text-green-400" : "text-red-400")">@r.ScorePercent%</div>
            <div class="text-arcade-muted text-sm">Score</div>
        </div>
        <div>
            <div class="text-3xl font-black">@r.CorrectAnswers / @r.TotalQuestions</div>
            <div class="text-arcade-muted text-sm">Correct</div>
        </div>
        <div>
            <div class="text-3xl font-black text-fire">@r.PassingScorePercent%</div>
            <div class="text-arcade-muted text-sm">To Pass</div>
        </div>
    </div>
</div>

<div class="space-y-2 mb-8">
    @foreach (var item in r.QuestionResults)
    {
        <div class="flex items-start gap-3 bg-arcade-card border border-arcade-border rounded-xl p-4">
            <span class="text-lg">@(item.IsCorrect ? "✅" : "❌")</span>
            <div class="flex-1">
                <p class="text-sm font-medium">@item.Question.Text</p>
                @if (item.SelectedOptions.Count > 0)
                {
                    <p class="text-sm mt-1 @(item.IsCorrect ? "text-green-400" : "text-red-400")">
                        Your answer: @string.Join(", ", item.SelectedOptions.Select(o => o.Text))
                    </p>
                }
                @if (!item.IsCorrect)
                {
                    var correctOptions = item.Question.AnswerOptions.Where(a => a.IsCorrect).ToList();
                    if (correctOptions.Count > 0)
                    {
                        <p class="text-sm text-green-400 mt-1">
                            Correct: @string.Join(", ", correctOptions.Select(o => o.Text))
                        </p>
                    }
                }
            </div>
        </div>
    }
</div>

<div class="flex gap-4">
    <a href="/exams" class="flex-1 text-center py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-fire transition">
        ← Back to Exams
    </a>
    <a href="/history" class="flex-1 text-center py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
        View History
    </a>
</div>
```

- [ ] **Step 2: Update `Review.cshtml`**

Replace `item.SelectedOption` (single) with `item.SelectedOptions` (list).

Full updated file:

```cshtml
@page "/sessions/{sessionId:int}/review"
@model Examageddon.Web.Pages.Sessions.ReviewModel
@{ ViewData["Title"] = "Review Answers"; }

<h1 class="text-3xl font-black mb-2">Review Your Answers</h1>
<p class="text-arcade-muted mb-8">Click any question to change your answer.</p>

<div class="space-y-3 mb-8">
    @foreach (var (item, i) in Model.Result.QuestionResults.Select((q, i) => (q, i)))
    {
        var position = i + 1;
        <a href="/sessions/@Model.SessionId/question/@position?fromReview=true"
           class="flex items-center gap-4 bg-arcade-card border border-arcade-border rounded-xl p-4 hover:border-fire transition">
            <span class="text-arcade-muted text-sm w-6 text-center font-bold">@position</span>
            <div class="flex-1">
                <p class="font-medium text-sm">@item.Question.Text</p>
                @if (item.SelectedOptions.Count > 0)
                {
                    <p class="text-fire text-sm mt-1">→ @string.Join(", ", item.SelectedOptions.Select(o => o.Text))</p>
                }
                else
                {
                    <p class="text-red-400 text-sm mt-1">⚠ Not answered</p>
                }
            </div>
            <span class="text-arcade-muted text-sm">Edit →</span>
        </a>
    }
</div>

<form method="post" action="/sessions/@Model.SessionId/complete">
    <button type="submit"
        class="w-full py-4 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-black text-lg rounded-xl hover:opacity-90 transition">
        Submit Exam 🏁
    </button>
</form>
```

- [ ] **Step 3: Build the full solution**

Run: `dotnet build src/Examageddon.slnx`

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Run all tests**

Run: `dotnet test src/Examageddon.Tests/`

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/Result.cshtml
git add src/Examageddon.Web/Pages/Sessions/Review.cshtml
git commit -m "feat: update Result and Review pages for multi-answer questions"
```

---

## Task 9: Reset dev database

The schema changed (new table, removed column, changed column type). The dev SQLite file must be deleted so `EnsureCreated()` rebuilds it on next run.

- [ ] **Step 1: Delete the dev database**

Run: `rm database/examageddon.db`

- [ ] **Step 2: Start the app and verify it starts cleanly**

Run: `dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj`

Expected: App starts, no exceptions, `database/examageddon.db` is recreated. Navigate to the manager, create a question with "Allow multiple correct answers" checked — verify the UI shows scoring mode options and checkboxes for correct answers.

- [ ] **Step 3: Commit**

```bash
git add -f database/.gitkeep 2>/dev/null; true
git commit -m "chore: reset dev database for multi-answer schema"
```

(If `database/examageddon.db` is gitignored there is nothing to commit here — skip the commit step.)
