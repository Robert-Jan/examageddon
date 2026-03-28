# Azure Certification Scoring Preset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `AzureCertification` scoring preset that stores a 1–1000 scaled score, requires 700 to pass, and awards multi-select partial credit without penalising wrong picks.

**Architecture:** `ScoringPreset` is an enum on `Exam` and snapshotted onto `ExamSession` at creation time. Scoring logic in `ExamSessionService` branches on the session's preset. `SessionResultModel` carries the preset so the result page can render `X / 1000` instead of `X%`. The exam creation form has an "Apply Azure Preset" button that pre-fills values via JavaScript.

**Tech Stack:** .NET 10, EF Core (SQLite via EnsureCreated), Razor Pages, HTMX, Tailwind CSS (CDN), XUnit

---

## File Map

| Action | File |
|---|---|
| Create | `src/Examageddon.Data/Enums/ScoringPreset.cs` |
| Modify | `src/Examageddon.Data/Entities/Exam.cs` |
| Modify | `src/Examageddon.Data/Entities/ExamSession.cs` |
| Modify | `src/Examageddon.Services/Models/SessionResultModel.cs` |
| Modify | `src/Examageddon.Services/ExamSessionService.cs` |
| Modify | `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml` |
| Modify | `src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs` |
| Modify | `src/Examageddon.Web/Pages/Sessions/Result.cshtml` |
| Modify | `src/Examageddon.Tests/ExamSessionServiceTests.cs` |
| Delete | `database/examageddon.db` |

---

## Task 1: Data foundation — enum, entity fields, rename, DB reset

All mechanical changes. No scoring behaviour changes yet. After this task the build passes and all existing tests pass.

**Files:**
- Create: `src/Examageddon.Data/Enums/ScoringPreset.cs`
- Modify: `src/Examageddon.Data/Entities/Exam.cs`
- Modify: `src/Examageddon.Data/Entities/ExamSession.cs`
- Modify: `src/Examageddon.Services/Models/SessionResultModel.cs`
- Modify: `src/Examageddon.Services/ExamSessionService.cs`
- Modify: `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml`
- Modify: `src/Examageddon.Web/Pages/Sessions/Result.cshtml`
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`
- Delete: `database/examageddon.db`

- [ ] **Step 1.1: Create `ScoringPreset` enum**

Create `src/Examageddon.Data/Enums/ScoringPreset.cs`:

```csharp
namespace Examageddon.Data.Enums;

public enum ScoringPreset
{
    None = 0,
    AzureCertification = 1,
}
```

- [ ] **Step 1.2: Update `Exam` entity**

Replace `src/Examageddon.Data/Entities/Exam.cs` with:

```csharp
using Examageddon.Data.Enums;

namespace Examageddon.Data.Entities;

public class Exam
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int PassingThreshold { get; set; }

    public int ExamQuestionCount { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public ICollection<Question> Questions { get; set; } = [];

    public ICollection<ExamSession> Sessions { get; set; } = [];
}
```

- [ ] **Step 1.3: Update `ExamSession` entity**

Add `ScoringPreset ScoringPreset` to `src/Examageddon.Data/Entities/ExamSession.cs`. The file already has `using Examageddon.Data.Enums;`. Add the property after `FeedbackMode`:

```csharp
    public FeedbackMode FeedbackMode { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public int TotalQuestions { get; set; }
```

- [ ] **Step 1.4: Update `SessionResultModel`**

Replace `src/Examageddon.Services/Models/SessionResultModel.cs` with:

```csharp
using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class SessionResultModel
{
    public int SessionId { get; set; }

    public string ExamTitle { get; set; } = string.Empty;

    public int TotalQuestions { get; set; }

    public double CorrectAnswers { get; set; }

    public int PassingThreshold { get; set; }

    public bool IsPassed { get; set; }

    public ScoringPreset ScoringPreset { get; set; }

    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round(CorrectAnswers / TotalQuestions * 100, 1);

    public int ScaledScore => TotalQuestions == 0 ? 0 : (int)Math.Round(CorrectAnswers / TotalQuestions * 1000);

    public List<QuestionResultItem> QuestionResults { get; set; } = [];
}
```

- [ ] **Step 1.5: Update `ExamSessionService` — renames + snapshot + result population**

Three changes in `src/Examageddon.Services/ExamSessionService.cs` (no scoring logic change yet):

**a) `CreateSessionAsync` — snapshot preset onto session:**

```csharp
        var session = new ExamSession
        {
            PersonId = personId,
            ExamId = examId,
            StartedAt = DateTime.UtcNow,
            QuestionMode = opts.QuestionMode,
            OrderMode = opts.OrderMode,
            FeedbackMode = opts.FeedbackMode,
            ScoringPreset = exam.ScoringPreset,
            TotalQuestions = questions.Count,
        };
```

**b) `CompleteSessionAsync` — rename `PassingScorePercent` → `PassingThreshold`:**

```csharp
        var totalScore = session.SessionAnswers.Sum(a => a.Score);
        var scorePercent = session.TotalQuestions == 0 ? 0 : totalScore / session.TotalQuestions * 100;

        session.CorrectAnswers = totalScore;
        session.IsPassed = scorePercent >= session.Exam.PassingThreshold;
        session.CompletedAt = DateTime.UtcNow;
        await sessionRepo.SaveChangesAsync();
```

**c) `GetResultAsync` — rename + add `ScoringPreset` to result model:**

```csharp
        return new SessionResultModel
        {
            SessionId = sessionId,
            ExamTitle = session.Exam.Title,
            TotalQuestions = session.TotalQuestions,
            CorrectAnswers = session.CorrectAnswers,
            PassingThreshold = session.Exam.PassingThreshold,
            IsPassed = session.IsPassed ?? false,
            ScoringPreset = session.ScoringPreset,
            QuestionResults = items,
        };
```

- [ ] **Step 1.6: Update `ExamForm.cshtml` — rename binding**

In `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml`, change the Passing Score block:

```html
        <div>
            <label class="block text-sm font-bold text-arcade-muted mb-2">Passing Score %</label>
            <input asp-for="Exam.PassingThreshold" type="number" min="1" max="1000" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
        </div>
```

(Change `asp-for="Exam.PassingScorePercent"` → `asp-for="Exam.PassingThreshold"` and `max="100"` → `max="1000"`.)

- [ ] **Step 1.7: Update `Result.cshtml` — rename**

In `src/Examageddon.Web/Pages/Sessions/Result.cshtml`, change `@r.PassingScorePercent` → `@r.PassingThreshold` (one occurrence in the "To Pass" cell):

```html
        <div>
            <div class="text-3xl font-black text-fire">@r.PassingThreshold%</div>
            <div class="text-arcade-muted text-sm">To Pass</div>
        </div>
```

- [ ] **Step 1.8: Update test seeds — rename `PassingScorePercent` → `PassingThreshold`**

In `src/Examageddon.Tests/ExamSessionServiceTests.cs`, update all three seed helpers. `PassingScorePercent = 60` → `PassingThreshold = 60`:

In `SeedAsync`:
```csharp
        var exam = new Exam { Title = "Demo", PassingThreshold = 60, ExamQuestionCount = 3 };
```

In `SeedMultiAnswerAsync`:
```csharp
        var exam = new Exam { Title = "Multi Demo", PassingThreshold = 60, ExamQuestionCount = 1 };
```

In `SeedDragAndDropAsync`:
```csharp
        var exam = new Exam { Title = "DnD Demo", PassingThreshold = 60, ExamQuestionCount = 1 };
```

- [ ] **Step 1.9: Delete the SQLite database file**

Run:
```bash
rm database/examageddon.db
```

(EnsureCreated will recreate it with the new schema on next app start.)

- [ ] **Step 1.10: Build**

Run:
```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded` with 0 errors. Fix any remaining `PassingScorePercent` references if the build reports them.

- [ ] **Step 1.11: Run tests**

Run:
```bash
dotnet test src/Examageddon.Tests/
```

Expected: all existing tests pass.

- [ ] **Step 1.12: Commit**

```bash
git add src/Examageddon.Data/Enums/ScoringPreset.cs src/Examageddon.Data/Entities/Exam.cs src/Examageddon.Data/Entities/ExamSession.cs src/Examageddon.Services/Models/SessionResultModel.cs src/Examageddon.Services/ExamSessionService.cs src/Examageddon.Web/Pages/Manage/ExamForm.cshtml src/Examageddon.Web/Pages/Sessions/Result.cshtml src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "feat: add ScoringPreset enum and entity fields, rename PassingScorePercent to PassingThreshold"
```

---

## Task 2: Write failing tests for Azure scoring behaviour

Tests for `SubmitAnswerAsync` (no-deduction partial credit) and `CompleteSessionAsync` (scaled 1–1000, pass at 700).

**Files:**
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 2.1: Add `SeedAzureAsync` helper**

Add a seed helper that creates an exam with `ScoringPreset.AzureCertification` and `PassingThreshold = 700`, with one multi-answer question (2 correct options, 1 wrong, `PartialCredit` scoring):

```csharp
    // Seeds an Azure-preset exam with one multi-answer question (2 correct, 1 wrong, PartialCredit)
    private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedAzureAsync()
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam
        {
            Title = "Azure Demo",
            PassingThreshold = 700,
            ExamQuestionCount = 3,
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
```

- [ ] **Step 2.2: Write test — Azure partial credit ignores wrong picks**

```csharp
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
```

- [ ] **Step 2.3: Write test — Azure complete session scales to 1000 and passes at 700**

```csharp
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
```

- [ ] **Step 2.4: Write test — Azure complete session fails below 700**

Add a seed helper for an Azure exam with 3 single-answer questions:

```csharp
    // Seeds an Azure-preset exam with 3 single-answer questions
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
```

Then the test:

```csharp
    [Fact]
    public async Task CompleteSession_Azure_TwoOfThreeCorrect_ScaledScore667_Fails()
    {
        // 2/3 correct → (int)Math.Round(2/3.0 * 1000) = 667 < 700 → fail
        var (ctx, person, exam) = await SeedAzureSingleAsync(3);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var svc = BuildService(ctx);
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sqs = ctx.SessionQuestions.Where(sq => sq.SessionId == session.Id).OrderBy(sq => sq.Position).ToList();

        // Answer first two correctly, leave third unanswered (score 0)
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
```

- [ ] **Step 2.5: Write test — Azure session snapshots preset**

```csharp
    [Fact]
    public async Task CreateSession_Azure_SnapshotsScoringPreset()
    {
        var (ctx, person, exam) = await SeedAzureSingleAsync(1);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };

        var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(ScoringPreset.AzureCertification, session.ScoringPreset);
    }
```

- [ ] **Step 2.6: Run tests to confirm new tests fail**

```bash
dotnet test src/Examageddon.Tests/ --filter "Azure"
```

Expected: all 4 new tests fail (the code doesn't implement Azure logic yet).

- [ ] **Step 2.7: Commit failing tests**

```bash
git add src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "test: add failing tests for Azure scoring preset"
```

---

## Task 3: Implement Azure scoring in `ExamSessionService`

**Files:**
- Modify: `src/Examageddon.Services/ExamSessionService.cs`

- [ ] **Step 3.1: Update `SubmitAnswerAsync` to load session and branch on preset**

In `ExamSessionService.cs`, replace the `SubmitAnswerAsync` method with:

```csharp
    public async Task SubmitAnswerAsync(int sessionId, int questionId, IReadOnlyList<int> answerOptionIds)
    {
        var question = await examRepo.GetQuestionAsync(questionId);
        if (question is null)
        {
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        var isAzure = session?.ScoringPreset == ScoringPreset.AzureCertification;

        var selectedIds = answerOptionIds.ToHashSet();
        var correctIds = question.AnswerOptions.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

        double score;
        bool isCorrect;

        if (!question.AllowMultipleAnswers || question.ScoringMode == MultiAnswerScoringMode.AllOrNothing)
        {
            isCorrect = selectedIds.SetEquals(correctIds);
            score = isCorrect ? 1.0 : 0.0;
        }
        else if (isAzure)
        {
            // Azure: each correct selection earns its point; wrong picks are ignored
            var correctSelected = selectedIds.Count(correctIds.Contains);
            score = correctIds.Count == 0
                ? 0.0
                : (double)correctSelected / correctIds.Count;
            isCorrect = score > 0;
        }
        else
        {
            var correctSelected = selectedIds.Count(correctIds.Contains);
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
```

- [ ] **Step 3.2: Update `CompleteSessionAsync` to branch on preset for pass/fail**

Replace the `CompleteSessionAsync` method with:

```csharp
    public async Task<SessionResultModel> CompleteSessionAsync(int sessionId)
    {
        var session = await sessionRepo.GetWithExamAndAnswersAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var totalScore = session.SessionAnswers.Sum(a => a.Score);
        session.CorrectAnswers = totalScore;

        if (session.ScoringPreset == ScoringPreset.AzureCertification)
        {
            var scaledScore = session.TotalQuestions == 0
                ? 0
                : (int)Math.Round(totalScore / session.TotalQuestions * 1000);
            session.IsPassed = scaledScore >= session.Exam.PassingThreshold;
        }
        else
        {
            var scorePercent = session.TotalQuestions == 0 ? 0 : totalScore / session.TotalQuestions * 100;
            session.IsPassed = scorePercent >= session.Exam.PassingThreshold;
        }

        session.CompletedAt = DateTime.UtcNow;
        await sessionRepo.SaveChangesAsync();

        return await GetResultAsync(sessionId);
    }
```

- [ ] **Step 3.3: Add `ScoringPreset` using directive if missing**

Ensure `ExamSessionService.cs` has `using Examageddon.Data.Enums;` at the top (it should already be there from the existing `QuestionMode`/`FeedbackMode` usage).

- [ ] **Step 3.4: Run tests**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all tests pass, including the 4 new Azure tests.

- [ ] **Step 3.5: Commit**

```bash
git add src/Examageddon.Services/ExamSessionService.cs
git commit -m "feat: implement Azure scoring — no-deduction partial credit and 1-1000 scaled pass/fail"
```

---

## Task 4: UI changes — exam form preset button, setup defaults, result display

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml`
- Modify: `src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs`
- Modify: `src/Examageddon.Web/Pages/Sessions/Result.cshtml`

- [ ] **Step 4.1: Add Azure preset button and hidden field to `ExamForm.cshtml`**

Replace `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml` with:

```html
@page "/manage/exams/{id:int}/edit"
@model Examageddon.Web.Pages.Manage.ExamFormModel
@using Examageddon.Data.Enums
@{ ViewData["Title"] = Model.IsEdit ? "Edit Exam" : "New Exam"; }

<h1 class="text-3xl font-black mb-8">@ViewData["Title"]</h1>

<form method="post" class="space-y-5 max-w-lg">
    <input type="hidden" asp-for="Exam.Id" />
    <input type="hidden" asp-for="Exam.ScoringPreset" id="ScoringPreset" />

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Title *</label>
        <input asp-for="Exam.Title" required
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
    </div>
    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Description</label>
        <textarea asp-for="Exam.Description" rows="3"
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire"></textarea>
    </div>
    <div class="grid grid-cols-2 gap-4">
        <div>
            <label class="block text-sm font-bold text-arcade-muted mb-2">Passing Threshold</label>
            <input asp-for="Exam.PassingThreshold" id="PassingThreshold" type="number" min="1" max="1000" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
        </div>
        <div>
            <label class="block text-sm font-bold text-arcade-muted mb-2">Exam Question Count</label>
            <input asp-for="Exam.ExamQuestionCount" id="ExamQuestionCount" type="number" min="1" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
        </div>
    </div>

    <div class="border border-arcade-border rounded-xl p-4 space-y-3">
        <p class="text-sm font-bold text-arcade-muted uppercase tracking-widest">Scoring Preset</p>
        <div class="flex items-center gap-3">
            <button type="button" onclick="applyAzurePreset()"
                class="px-4 py-2 bg-arcade-card border border-arcade-border rounded-lg text-sm hover:border-fire transition">
                Apply Azure Certification
            </button>
            <span id="azure-badge"
                class="@(Model.Exam.ScoringPreset == ScoringPreset.AzureCertification ? "" : "hidden") text-xs font-bold text-fire border border-fire rounded px-2 py-1">
                Azure Preset Active
            </span>
        </div>
        <p class="text-xs text-arcade-muted">Sets passing threshold to 700 / 1000, question count to 40, and enables Azure-style partial credit scoring.</p>
    </div>

    <div class="flex gap-4 pt-2">
        <button type="submit"
            class="px-8 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
            @(Model.IsEdit ? "Save Changes" : "Create Exam")
        </button>
        <a href="/manage" class="px-8 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
            Cancel
        </a>
    </div>
</form>

<script>
    function applyAzurePreset() {
        document.getElementById('PassingThreshold').value = '700';
        document.getElementById('ExamQuestionCount').value = '40';
        document.getElementById('ScoringPreset').value = '1';
        document.getElementById('azure-badge').classList.remove('hidden');
    }
</script>
```

- [ ] **Step 4.2: Pre-fill session setup defaults from exam preset in `Setup.cshtml.cs`**

In `src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs`, update `OnGetAsync` to pre-select Azure defaults when the exam uses the Azure preset. Add `using Examageddon.Data.Enums;` at the top if not present.

Replace the `OnGetAsync` method with:

```csharp
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        var exam = await examService.GetExamAsync(id);
        if (exam is null)
        {
            return NotFound();
        }

        Exam = exam;

        if (exam.ScoringPreset == ScoringPreset.AzureCertification)
        {
            QuestionMode = QuestionMode.Limited;
            OrderMode = OrderMode.Random;
            FeedbackMode = FeedbackMode.AtEnd;
        }

        return Page();
    }
```

- [ ] **Step 4.3: Update `Result.cshtml` for conditional Azure score display**

Replace `src/Examageddon.Web/Pages/Sessions/Result.cshtml` with:

```html
@page "/sessions/{sessionId:int}/result"
@model Examageddon.Web.Pages.Sessions.ResultModel
@using Examageddon.Data.Enums
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
        @if (r.ScoringPreset == ScoringPreset.AzureCertification)
        {
            <div>
                <div class="text-3xl font-black @(r.IsPassed ? "text-green-400" : "text-red-400")">@r.ScaledScore / 1000</div>
                <div class="text-arcade-muted text-sm">Score</div>
            </div>
            <div>
                <div class="text-3xl font-black">@r.CorrectAnswers / @r.TotalQuestions</div>
                <div class="text-arcade-muted text-sm">Correct</div>
            </div>
            <div>
                <div class="text-3xl font-black text-fire">@r.PassingThreshold / 1000</div>
                <div class="text-arcade-muted text-sm">To Pass (scaled)</div>
            </div>
        }
        else
        {
            <div>
                <div class="text-3xl font-black @(r.IsPassed ? "text-green-400" : "text-red-400")">@r.ScorePercent%</div>
                <div class="text-arcade-muted text-sm">Score</div>
            </div>
            <div>
                <div class="text-3xl font-black">@r.CorrectAnswers / @r.TotalQuestions</div>
                <div class="text-arcade-muted text-sm">Correct</div>
            </div>
            <div>
                <div class="text-3xl font-black text-fire">@r.PassingThreshold%</div>
                <div class="text-arcade-muted text-sm">To Pass</div>
            </div>
        }
    </div>
</div>

<div class="space-y-2 mb-8">
    @foreach (var item in r.QuestionResults)
    {
        <div class="flex items-start gap-3 bg-arcade-card border border-arcade-border rounded-xl p-4">
            <span class="text-lg">@(item.IsCorrect ? "✅" : "❌")</span>
            <div class="flex-1">
                <p class="text-sm font-medium">@item.Question.Text</p>
                @if (item.Question.QuestionType == QuestionType.DragAndDrop)
                {
                    @foreach (var slot in item.Question.AnswerOptions.OrderBy(a => a.OrderIndex))
                    {
                        var hasPlaced = item.DragPlacements.TryGetValue(slot.Id, out var placedOpt);
                        var placedLabel = hasPlaced ? placedOpt!.MatchText ?? "" : "—";
                        var isCorrect = hasPlaced && placedOpt!.Id == slot.Id;
                        <p class="text-sm mt-1 @(isCorrect ? "text-green-400" : "text-red-400")">
                            @(isCorrect ? "✓" : "✗") @placedLabel → @slot.Text
                            @if (!isCorrect && slot.MatchText is not null)
                            {
                                <span class="text-green-400"> (correct: @slot.MatchText)</span>
                            }
                        </p>
                    }
                }
                else
                {
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

- [ ] **Step 4.4: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4.5: Run all tests**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all tests pass.

- [ ] **Step 4.6: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/ExamForm.cshtml src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs src/Examageddon.Web/Pages/Sessions/Result.cshtml
git commit -m "feat: add Azure preset button to exam form, pre-fill setup defaults, show scaled score on result page"
```
