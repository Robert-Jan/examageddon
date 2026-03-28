# Drag & Drop Questions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a DragAndDrop question type where students drag shuffled labels onto matching sentences, with all-or-nothing or partial credit scoring.

**Architecture:** Extend `AnswerOption` with `MatchText` (the draggable label) and `SessionAnswerSelection` with `PlacedOptionId` (what was actually dropped in a slot). A new service method `SubmitDragAnswerAsync` handles scoring. The session UI uses the HTML5 drag API with a tap-to-select touch fallback, all in vanilla JS.

**Tech Stack:** .NET 10, EF Core / SQLite (`EnsureCreated`), Razor Pages, HTMX, Tailwind CSS (CDN), XUnit.

---

## File Map

| File | Change |
|---|---|
| `src/Examageddon.Data/Enums/QuestionType.cs` | Add `DragAndDrop = 2` |
| `src/Examageddon.Data/Entities/AnswerOption.cs` | Add `string? MatchText` |
| `src/Examageddon.Data/Entities/SessionAnswerSelection.cs` | Add `int? PlacedOptionId` |
| `src/Examageddon.Services/Models/SessionQuestionModel.cs` | Add `IReadOnlyDictionary<int, int> DragPlacements` |
| `src/Examageddon.Services/Models/QuestionResultItem.cs` | Add `IReadOnlyDictionary<int, AnswerOption> DragPlacements` |
| `src/Examageddon.Services/ExamSessionService.cs` | Add `SubmitDragAnswerAsync`; update `GetSessionQuestionAsync` to shuffle + populate `DragPlacements`; update `GetResultAsync` to populate `DragPlacements` |
| `src/Examageddon.Tests/ExamSessionServiceTests.cs` | Add 4 DragAndDrop scoring tests + seed helper |
| `src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs` | Add `OnPostDragAnswerAsync` handler |
| `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml` | Add DragAndDrop branch (unanswered + answered states) |
| `src/Examageddon.Web/Pages/Sessions/Result.cshtml` | Add DragAndDrop rendering branch |
| `src/Examageddon.Web/Pages/Sessions/Review.cshtml` | Add DragAndDrop summary line |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` | Add Drag & Drop radio + pairs UI + JS |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs` | Add `MatchTexts[]` binding; handle DragAndDrop on POST |

---

## Task 1: Data model additions

**Files:**
- Modify: `src/Examageddon.Data/Enums/QuestionType.cs`
- Modify: `src/Examageddon.Data/Entities/AnswerOption.cs`
- Modify: `src/Examageddon.Data/Entities/SessionAnswerSelection.cs`

- [ ] **Step 1: Add `DragAndDrop` to the enum**

Replace the contents of `src/Examageddon.Data/Enums/QuestionType.cs`:

```csharp
namespace Examageddon.Data.Enums;

public enum QuestionType
{
    MultipleChoice = 0,
    Statement = 1,
    DragAndDrop = 2,
}
```

- [ ] **Step 2: Add `MatchText` to `AnswerOption`**

In `src/Examageddon.Data/Entities/AnswerOption.cs`, add after the `IsCorrect` property:

```csharp
public string? MatchText { get; set; }
```

The file should look like:

```csharp
namespace Examageddon.Data.Entities;

public class AnswerOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string? MatchText { get; set; }
    public int OrderIndex { get; set; }
}
```

- [ ] **Step 3: Add `PlacedOptionId` to `SessionAnswerSelection`**

In `src/Examageddon.Data/Entities/SessionAnswerSelection.cs`, add after `AnswerOptionId`:

```csharp
public int? PlacedOptionId { get; set; }
```

The file should look like:

```csharp
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
```

- [ ] **Step 4: Delete the dev database so EnsureCreated rebuilds with new columns**

```bash
rm database/examageddon.db
```

(The file is auto-recreated on next app start. Tests use in-memory SQLite and are unaffected.)

- [ ] **Step 5: Build to confirm no compile errors**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Data/Enums/QuestionType.cs src/Examageddon.Data/Entities/AnswerOption.cs src/Examageddon.Data/Entities/SessionAnswerSelection.cs
git commit -m "feat: add DragAndDrop enum value, MatchText and PlacedOptionId columns"
```

---

## Task 2: Service model additions

**Files:**
- Modify: `src/Examageddon.Services/Models/SessionQuestionModel.cs`
- Modify: `src/Examageddon.Services/Models/QuestionResultItem.cs`

- [ ] **Step 1: Add `DragPlacements` to `SessionQuestionModel`**

In `src/Examageddon.Services/Models/SessionQuestionModel.cs`, add after `IsFromReview`:

```csharp
public IReadOnlyDictionary<int, int> DragPlacements { get; set; } = new Dictionary<int, int>();
```

Full file:

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
    public bool IsAnswered { get; set; }
    public bool AllowMultipleAnswers { get; set; }
    public QuestionType QuestionType { get; set; }
    public bool IsFromReview { get; set; }
    public IReadOnlyDictionary<int, int> DragPlacements { get; set; } = new Dictionary<int, int>();
}
```

- [ ] **Step 2: Add `DragPlacements` to `QuestionResultItem`**

In `src/Examageddon.Services/Models/QuestionResultItem.cs`, add after `IsAnswered`:

```csharp
public IReadOnlyDictionary<int, AnswerOption> DragPlacements { get; set; } = new Dictionary<int, AnswerOption>();
```

Full file:

```csharp
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
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Services/Models/SessionQuestionModel.cs src/Examageddon.Services/Models/QuestionResultItem.cs
git commit -m "feat: add DragPlacements to session and result models"
```

---

## Task 3: Tests for SubmitDragAnswerAsync (write failing)

**Files:**
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 1: Add seed helper `SeedDragAndDropAsync` and four tests**

Add the following at the end of `ExamSessionServiceTests.cs`, before the final `}`:

```csharp
[Fact]
public async Task SubmitDragAnswer_AllCorrect_PartialCredit_ScoresOne()
{
    var (ctx, person, exam) = await SeedDragAndDropAsync(scoringMode: MultiAnswerScoringMode.PartialCredit);
    var svc = BuildService(ctx);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

    var sq = ctx.SessionQuestions.First(sq => sq.SessionId == session.Id);
    var slots = ctx.AnswerOptions.Where(a => a.QuestionId == sq.QuestionId).OrderBy(a => a.OrderIndex).ToList();
    var slotIds = slots.Select(a => a.Id).ToList();
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
    var slotIds = slots.Select(a => a.Id).ToList();
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
    var slotIds = slots.Select(a => a.Id).ToList();
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
    var slotIds = slots.Select(a => a.Id).ToList();
    var placedIds = new List<int> { slots[0].Id, slots[0].Id }; // 1 of 2 correct

    await svc.SubmitDragAnswerAsync(session.Id, sq.QuestionId, slotIds, placedIds);

    var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
    Assert.Equal(0.0, answer.Score);
    Assert.False(answer.IsCorrect);
}

// Seeds an exam with one DragAndDrop question (2 pairs: Paris/France, Berlin/Germany)
private static async Task<(ExamageddonDbContext Ctx, Person Person, Exam Exam)> SeedDragAndDropAsync(
    MultiAnswerScoringMode scoringMode = MultiAnswerScoringMode.AllOrNothing)
{
    var ctx = TestDbContextFactory.Create();
    var person = new Person { Name = "Tester" };
    var exam = new Exam { Title = "DnD Demo", PassingScorePercent = 60, ExamQuestionCount = 1 };

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
```

- [ ] **Step 2: Run tests — expect failures because `SubmitDragAnswerAsync` does not exist yet**

```bash
dotnet test src/Examageddon.Tests/ --filter "SubmitDragAnswer"
```

Expected: compile error `'ExamSessionService' does not contain a definition for 'SubmitDragAnswerAsync'`

---

## Task 4: Implement `SubmitDragAnswerAsync` and update `GetSessionQuestionAsync` / `GetResultAsync`

**Files:**
- Modify: `src/Examageddon.Services/ExamSessionService.cs`

- [ ] **Step 1: Add `SubmitDragAnswerAsync` method**

Add the following method to `ExamSessionService` after `SubmitAnswerAsync`:

```csharp
public async Task SubmitDragAnswerAsync(
    int sessionId,
    int questionId,
    IReadOnlyList<int> slotIds,
    IReadOnlyList<int> placedIds)
{
    var question = await examRepo.GetQuestionAsync(questionId);
    if (question is null) return;

    var totalPairs = slotIds.Count;
    var correctCount = slotIds.Where((slotId, i) => i < placedIds.Count && placedIds[i] == slotId).Count();

    double score;
    bool isCorrect;

    if (question.ScoringMode == MultiAnswerScoringMode.AllOrNothing)
    {
        isCorrect = correctCount == totalPairs;
        score = isCorrect ? 1.0 : 0.0;
    }
    else
    {
        score = totalPairs == 0 ? 0.0 : (double)correctCount / totalPairs;
        isCorrect = score > 0;
    }

    var selections = slotIds
        .Select((slotId, i) => new SessionAnswerSelection
        {
            AnswerOptionId = slotId,
            PlacedOptionId = i < placedIds.Count ? placedIds[i] : null,
        })
        .ToList();

    var existing = await sessionRepo.GetAnswerAsync(sessionId, questionId);
    if (existing is not null)
    {
        existing.Score = score;
        existing.IsCorrect = isCorrect;
        existing.AnsweredAt = DateTime.UtcNow;
        existing.Selections.Clear();
        foreach (var sel in selections) existing.Selections.Add(sel);
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

- [ ] **Step 2: Update `GetSessionQuestionAsync` to shuffle and populate `DragPlacements`**

Replace the `return new SessionQuestionModel { ... }` block in `GetSessionQuestionAsync` with:

```csharp
return new SessionQuestionModel
{
    SessionId = sessionId,
    Position = position,
    TotalQuestions = session.TotalQuestions,
    FeedbackMode = session.FeedbackMode,
    Question = sq.Question,
    AnswerOptions = sq.Question.QuestionType == QuestionType.DragAndDrop
        ? [.. sq.Question.AnswerOptions.OrderBy(_ => Guid.NewGuid())]
        : [.. sq.Question.AnswerOptions.OrderBy(a => a.OrderIndex)],
    SelectedAnswerOptionIds = answer?.Selections.Select(s => s.AnswerOptionId).ToHashSet() ?? [],
    IsAnswered = answer is not null,
    AllowMultipleAnswers = sq.Question.AllowMultipleAnswers,
    QuestionType = sq.Question.QuestionType,
    IsFromReview = fromReview,
    DragPlacements = sq.Question.QuestionType == QuestionType.DragAndDrop && answer is not null
        ? answer.Selections
            .Where(s => s.PlacedOptionId.HasValue)
            .ToDictionary(s => s.AnswerOptionId, s => s.PlacedOptionId!.Value)
        : new Dictionary<int, int>(),
};
```

- [ ] **Step 3: Update `GetResultAsync` to populate `DragPlacements` on `QuestionResultItem`**

In `GetResultAsync`, the `.Select(sq => { ... })` block builds `QuestionResultItem`. Replace it with:

```csharp
var items = session.SessionQuestions.OrderBy(sq => sq.Position).Select(sq =>
{
    var answer = session.SessionAnswers.FirstOrDefault(sa => sa.QuestionId == sq.QuestionId);
    var dragPlacements = sq.Question.QuestionType == QuestionType.DragAndDrop && answer is not null
        ? answer.Selections
            .Where(s => s.PlacedOptionId.HasValue)
            .ToDictionary(
                s => s.AnswerOptionId,
                s => sq.Question.AnswerOptions.FirstOrDefault(a => a.Id == s.PlacedOptionId!.Value)!)
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value)
        : (IReadOnlyDictionary<int, AnswerOption>)new Dictionary<int, AnswerOption>();

    return new QuestionResultItem
    {
        Question = sq.Question,
        SelectedOptions = answer?.Selections.Select(s => s.AnswerOption).ToList() ?? [],
        IsCorrect = answer?.IsCorrect ?? false,
        IsAnswered = answer is not null,
        DragPlacements = dragPlacements,
    };
}).ToList();
```

- [ ] **Step 4: Run the new tests**

```bash
dotnet test src/Examageddon.Tests/ --filter "SubmitDragAnswer"
```

Expected: all 4 pass.

- [ ] **Step 5: Run all tests to confirm nothing regressed**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Services/ExamSessionService.cs src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "feat: implement SubmitDragAnswerAsync with scoring and DragPlacements population"
```

---

## Task 5: Web handler for drag answer submission

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs`

- [ ] **Step 1: Add `OnPostDragAnswerAsync` to `QuestionModel`**

Add after `OnPostAnswerAsync`:

```csharp
public async Task<IActionResult> OnPostDragAnswerAsync(int sessionId, int n, int questionId, List<int> slotIds, List<int> placedIds)
{
    if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        return RedirectToPage("/Index");

    await sessionService.SubmitDragAnswerAsync(sessionId, questionId, slotIds, placedIds);
    SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n);
    if (SessionQuestion is null) return NotFound();

    return Partial("_AnswerFeedback", SessionQuestion);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs
git commit -m "feat: add OnPostDragAnswerAsync handler to QuestionModel"
```

---

## Task 6: Session UI — DragAndDrop in `_AnswerFeedback.cshtml`

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`

- [ ] **Step 1: Add the DragAndDrop branch at the top of `_AnswerFeedback.cshtml`**

Insert the following block at the very top, before `@if (Model.QuestionType == QuestionType.Statement)`:

```cshtml
@if (Model.QuestionType == QuestionType.DragAndDrop)
{
    @if (!Model.IsAnswered)
    {
        var slotOptions = Model.Question.AnswerOptions.OrderBy(a => a.OrderIndex).ToList();
        <div id="dnd-container">
            <!-- Pool -->
            <div class="mb-4">
                <p class="text-xs text-arcade-muted mb-2 uppercase tracking-wide font-bold">Drag to match</p>
                <div id="dnd-pool" class="flex flex-wrap gap-2 min-h-10 p-3 bg-arcade-dark border border-dashed border-arcade-border rounded-xl">
                    @foreach (var opt in Model.AnswerOptions)
                    {
                        var preplacedSlot = Model.DragPlacements.Values.Contains(opt.Id) ? (int?)Model.DragPlacements.FirstOrDefault(kv => kv.Value == opt.Id).Key : null;
                        if (preplacedSlot is null)
                        {
                            <div class="dnd-chip cursor-grab select-none px-3 py-1 bg-arcade-card border-2 border-dashed border-fire text-fire text-sm font-semibold rounded-lg"
                                 draggable="true"
                                 data-option-id="@opt.Id"
                                 data-label="@opt.MatchText">
                                @opt.MatchText
                            </div>
                        }
                    }
                </div>
            </div>

            <!-- Slots -->
            <div class="space-y-2">
                @foreach (var slot in slotOptions)
                {
                    var preplacedId = Model.DragPlacements.TryGetValue(slot.Id, out var pid) ? pid : (int?)null;
                    var preplacedLabel = preplacedId.HasValue
                        ? Model.Question.AnswerOptions.FirstOrDefault(a => a.Id == preplacedId.Value)?.MatchText ?? ""
                        : null;
                    <div class="flex items-center gap-3">
                        <div class="dnd-slot w-28 min-h-9 flex items-center justify-center border-2 rounded-lg flex-shrink-0 transition"
                             data-slot-id="@slot.Id"
                             data-placed-id="@(preplacedId?.ToString() ?? "")"
                             ondragover="event.preventDefault()"
                             ondrop="dndDrop(event, this)">
                            @if (preplacedLabel is not null)
                            {
                                <div class="dnd-placed-chip w-full text-center px-2 py-1 text-fire text-sm font-semibold"
                                     draggable="true"
                                     data-option-id="@preplacedId"
                                     data-label="@preplacedLabel">
                                    @preplacedLabel
                                </div>
                            }
                            else
                            {
                                <span class="text-xs text-arcade-muted">drop here</span>
                            }
                        </div>
                        <span class="text-arcade-text">@slot.Text</span>
                    </div>
                }
            </div>

            <!-- Hidden form inputs + submit -->
            <form id="dnd-form" class="mt-4">
                @foreach (var slot in slotOptions)
                {
                    <input type="hidden" class="dnd-slot-input" data-slot-id="@slot.Id" name="slotIds" value="@slot.Id" />
                    <input type="hidden" class="dnd-placed-input" data-slot-id="@slot.Id" name="placedIds" value="" />
                }
            </form>
            <button id="dnd-submit" type="button" disabled
                    hx-post="/sessions/@Model.SessionId/question/@Model.Position?handler=DragAnswer&questionId=@Model.Question.Id"
                    hx-include="#dnd-form"
                    hx-target="#answer-container"
                    hx-swap="innerHTML"
                    class="mt-2 w-full px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl opacity-50 cursor-not-allowed transition">
                Submit Answer
            </button>
        </div>

        <script>
        (function () {
            var _selected = null; // touch: currently selected chip node

            function getPool() { return document.getElementById('dnd-pool'); }

            function updateSubmitState() {
                var slots = document.querySelectorAll('#dnd-container .dnd-slot');
                var allFilled = Array.from(slots).every(s => s.dataset.placedId !== '');
                var btn = document.getElementById('dnd-submit');
                btn.disabled = !allFilled;
                btn.classList.toggle('opacity-50', !allFilled);
                btn.classList.toggle('cursor-not-allowed', !allFilled);
            }

            function syncHiddenInput(slotId, placedId) {
                var input = document.querySelector('.dnd-placed-input[data-slot-id="' + slotId + '"]');
                if (input) input.value = placedId || '';
            }

            function placeChip(chip, slotEl) {
                // remove existing chip from slot → back to pool
                var existing = slotEl.querySelector('.dnd-placed-chip');
                if (existing) returnToPool(existing);

                // move chip from wherever it is into the slot
                chip.classList.remove('dnd-chip');
                chip.classList.add('dnd-placed-chip');
                chip.setAttribute('draggable', 'true');
                slotEl.innerHTML = '';
                slotEl.appendChild(chip);
                slotEl.dataset.placedId = chip.dataset.optionId;
                syncHiddenInput(slotEl.dataset.slotId, chip.dataset.optionId);
                updateSubmitState();
            }

            function returnToPool(chip) {
                chip.classList.remove('dnd-placed-chip');
                chip.classList.add('dnd-chip');
                var slot = chip.closest('.dnd-slot');
                if (slot) {
                    slot.dataset.placedId = '';
                    slot.innerHTML = '<span class="text-xs text-arcade-muted">drop here</span>';
                    syncHiddenInput(slot.dataset.slotId, '');
                }
                getPool().appendChild(chip);
                updateSubmitState();
            }

            // ── Drag API ──────────────────────────────────────────────
            document.addEventListener('dragstart', function (e) {
                var chip = e.target.closest('.dnd-chip, .dnd-placed-chip');
                if (!chip) return;
                e.dataTransfer.setData('text/plain', chip.dataset.optionId);
                e.dataTransfer.effectAllowed = 'move';
                chip._dragSrc = true;
            });

            document.addEventListener('dragend', function (e) {
                var chip = e.target.closest('.dnd-chip, .dnd-placed-chip');
                if (chip) chip._dragSrc = false;
            });

            window.dndDrop = function (e, slotEl) {
                e.preventDefault();
                var optionId = e.dataTransfer.getData('text/plain');
                var chip = document.querySelector('[data-option-id="' + optionId + '"]');
                if (!chip) return;
                placeChip(chip, slotEl);
            };

            // ── Touch fallback ────────────────────────────────────────
            document.addEventListener('touchstart', function (e) {
                var chip = e.target.closest('.dnd-chip');
                var placedChip = e.target.closest('.dnd-placed-chip');
                var slot = e.target.closest('.dnd-slot');

                if (chip) {
                    // select from pool
                    if (_selected) _selected.classList.remove('ring-2', 'ring-white');
                    _selected = chip;
                    chip.classList.add('ring-2', 'ring-white');
                    e.preventDefault();
                    return;
                }

                if (placedChip) {
                    // return placed chip to pool
                    if (_selected) _selected.classList.remove('ring-2', 'ring-white');
                    _selected = null;
                    returnToPool(placedChip);
                    e.preventDefault();
                    return;
                }

                if (slot && _selected) {
                    // place selected chip in slot
                    _selected.classList.remove('ring-2', 'ring-white');
                    placeChip(_selected, slot);
                    _selected = null;
                    e.preventDefault();
                    return;
                }
            }, { passive: false });

            updateSubmitState();
        })();
        </script>
    }
    else
    {
        var showDndFeedback = Model.FeedbackMode == FeedbackMode.Direct;
        var slotOptions = Model.Question.AnswerOptions.OrderBy(a => a.OrderIndex).ToList();
        <div class="space-y-2">
            @foreach (var slot in slotOptions)
            {
                var hasPlaced = Model.DragPlacements.TryGetValue(slot.Id, out var placedOpt);
                var placedLabel = hasPlaced ? placedOpt!.MatchText ?? "" : "";
                var isSlotCorrect = hasPlaced && placedOpt!.Id == slot.Id;

                string borderClass, bgClass, textClass;
                if (showDndFeedback)
                {
                    if (isSlotCorrect)
                    { borderClass = "border-green-500"; bgClass = "bg-green-500/10"; textClass = "text-green-400"; }
                    else
                    { borderClass = "border-red-500"; bgClass = "bg-red-500/10"; textClass = "text-red-400"; }
                }
                else
                { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-text"; }

                <div class="flex items-center gap-3 border @borderClass @bgClass rounded-xl p-3">
                    @if (showDndFeedback)
                    {
                        if (isSlotCorrect) { <span class="text-green-400 flex-shrink-0 w-4">✓</span> }
                        else { <span class="text-red-400 flex-shrink-0 w-4">✗</span> }
                    }
                    <div class="flex flex-col gap-1 flex-shrink-0 w-24">
                        <span class="px-2 py-0.5 border @(showDndFeedback && !isSlotCorrect ? "border-red-500 text-red-400" : "border-fire text-fire") rounded-md text-sm font-semibold text-center">
                            @placedLabel
                        </span>
                        @if (showDndFeedback && !isSlotCorrect)
                        {
                            <span class="px-2 py-0.5 border border-green-500 text-green-400 rounded-md text-sm font-semibold text-center">
                                @slot.MatchText ✓
                            </span>
                        }
                    </div>
                    <span class="@textClass">@slot.Text</span>
                </div>
            }
        </div>
    }
}
else if (Model.QuestionType == QuestionType.Statement)
```

Note: that last line (`else if (Model.QuestionType == QuestionType.Statement)`) replaces the existing opening `@if (Model.QuestionType == QuestionType.Statement)` line. The rest of `_AnswerFeedback.cshtml` is unchanged.

- [ ] **Step 2: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run all tests**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml
git commit -m "feat: add DragAndDrop session UI with drag API and touch fallback"
```

---

## Task 7: Result page — DragAndDrop rendering

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/Result.cshtml`

- [ ] **Step 1: Add DragAndDrop-specific rendering in the result item loop**

The result page currently has a loop `@foreach (var item in r.QuestionResults)`. Inside that loop, there is a `<div>` showing `item.Question.Text` and `item.SelectedOptions`. Add a DragAndDrop branch.

Replace the inner `<div class="flex-1">` block (lines 42–59 in the current file) with:

```cshtml
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
```

Also add `@using Examageddon.Data.Enums` at the top of `Result.cshtml` if not already present (it currently doesn't have it — add it after the `@model` line).

- [ ] **Step 2: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/Result.cshtml
git commit -m "feat: add DragAndDrop result rendering with per-slot correct/wrong display"
```

---

## Task 8: Review page — DragAndDrop summary line

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/Review.cshtml`

- [ ] **Step 1: Add DragAndDrop summary in the review item loop**

In `Review.cshtml`, the loop renders `item.SelectedOptions` as a summary. Replace the inner answer summary block:

```cshtml
@if (item.SelectedOptions.Count > 0)
{
    <p class="text-fire text-sm mt-1">→ @string.Join(", ", item.SelectedOptions.Select(o => o.Text))</p>
}
else
{
    <p class="text-red-400 text-sm mt-1">⚠ Not answered</p>
}
```

With:

```cshtml
@if (item.Question.QuestionType == QuestionType.DragAndDrop)
{
    @if (item.DragPlacements.Count > 0)
    {
        var summary = string.Join(", ", item.Question.AnswerOptions
            .OrderBy(a => a.OrderIndex)
            .Where(slot => item.DragPlacements.ContainsKey(slot.Id))
            .Select(slot => $"{item.DragPlacements[slot.Id].MatchText} → {slot.Text}"));
        <p class="text-fire text-sm mt-1">→ @summary</p>
    }
    else
    {
        <p class="text-red-400 text-sm mt-1">⚠ Not answered</p>
    }
}
else if (item.SelectedOptions.Count > 0)
{
    <p class="text-fire text-sm mt-1">→ @string.Join(", ", item.SelectedOptions.Select(o => o.Text))</p>
}
else
{
    <p class="text-red-400 text-sm mt-1">⚠ Not answered</p>
}
```

Also add `@using Examageddon.Data.Enums` at the top of `Review.cshtml` if not already present.

- [ ] **Step 2: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/Review.cshtml
git commit -m "feat: add DragAndDrop summary line to review page"
```

---

## Task 9: Manager form — backend

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs`

- [ ] **Step 1: Add `MatchTexts` binding property**

Add after `OptionTexts`:

```csharp
[BindProperty]
public List<string> MatchTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];
```

- [ ] **Step 2: Populate `MatchTexts` on `OnGetAsync` for edit mode**

Inside the `if (questionId.HasValue && questionId.Value != 0)` block in `OnGetAsync`, add after the existing `OptionTexts = ...` and `CorrectOptionIndices = ...` lines:

```csharp
MatchTexts = [.. ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.MatchText ?? string.Empty)];
```

- [ ] **Step 3: Handle DragAndDrop in `OnPostAsync`**

In `OnPostAsync`, the options are built from `OptionTexts`. For DragAndDrop, also set `MatchText`. Replace the `var options = OptionTexts.Select(...)` block with:

```csharp
var options = OptionTexts
    .Select((text, idx) => new AnswerOption
    {
        Text = text,
        MatchText = QuestionType == QuestionType.DragAndDrop && idx < MatchTexts.Count
            ? MatchTexts[idx]
            : null,
        IsCorrect = QuestionType == QuestionType.DragAndDrop
            ? !string.IsNullOrWhiteSpace(text) && idx < MatchTexts.Count && !string.IsNullOrWhiteSpace(MatchTexts[idx])
            : CorrectOptionIndices.Contains(idx),
        OrderIndex = idx,
    })
    .Where(a => !string.IsNullOrWhiteSpace(a.Text))
    .ToList();
```

- [ ] **Step 4: Add DragAndDrop validation + set flags in `OnPostAsync`**

Add a DragAndDrop branch alongside the existing `if (QuestionType == QuestionType.Statement)` block:

```csharp
if (QuestionType == QuestionType.DragAndDrop)
{
    if (options.Count == 0 || options.Any(o => string.IsNullOrWhiteSpace(o.MatchText)))
    {
        ModelState.AddModelError(string.Empty, "Each pair must have both a label and a sentence.");
        ExamId = id;
        return Page();
    }
    AllowMultipleAnswers = true;
}
else if (QuestionType == QuestionType.Statement)
```

Note: the existing `else if (QuestionType == QuestionType.Statement)` block remains unchanged after this new `if` block; just convert the `if` to `else if`.

- [ ] **Step 5: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Run all tests**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs
git commit -m "feat: add MatchTexts binding and DragAndDrop handling to QuestionFormModel"
```

---

## Task 10: Manager form — UI

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`

- [ ] **Step 1: Add the Drag & Drop radio button**

In the question type radio group, after the Statement radio, add:

```cshtml
<label class="flex items-center gap-2 cursor-pointer">
    <input type="radio" name="QuestionType" value="@((int)QuestionType.DragAndDrop)"
           @(Model.QuestionType == QuestionType.DragAndDrop ? "checked" : "")
           onchange="switchQuestionType(this.value)"
           class="accent-yellow-500" />
    <span class="text-sm text-arcade-text">Drag &amp; Drop</span>
</label>
```

- [ ] **Step 2: Add MatchText inputs to each option row**

Each option row currently has a single text input for `OptionTexts[i]`. For DragAndDrop, we need a second input for `MatchTexts[i]`. Wrap the existing input with a label column div and add the second input.

Replace each option row's content inside `#options-container` with a structure that adds a `MatchText` input. Change the existing option row template (both the Razor server-rendered rows and the JS `addOption` function).

For the server-rendered rows (inside `@for (int i = 0; i < Model.OptionTexts.Count; i++)`), replace the content with:

```cshtml
<div class="option-row flex items-center gap-3">
    <input type="checkbox" name="CorrectOptionIndices" value="@i"
           @(Model.CorrectOptionIndices.Contains(i) ? "checked" : "")
           class="correct-input accent-yellow-500 w-4 h-4 flex-shrink-0 mc-only @(Model.QuestionType == QuestionType.Statement || Model.QuestionType == QuestionType.DragAndDrop ? "hidden" : "")" />
    <input type="text" name="MatchTexts[@i]" value="@(Model.MatchTexts.Count > i ? Model.MatchTexts[i] : "")"
           placeholder="Label @(i + 1)"
           class="match-text flex-shrink-0 w-32 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire dnd-only @(Model.QuestionType == QuestionType.DragAndDrop ? "" : "hidden")" />
    <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
           placeholder="@(Model.QuestionType == QuestionType.Statement ? $"Statement {i + 1}" : Model.QuestionType == QuestionType.DragAndDrop ? $"Sentence {i + 1}" : $"Option {i + 1}")"
           class="option-text flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
    <div class="stmt-only @(Model.QuestionType == QuestionType.Statement ? "" : "hidden") flex gap-1">
        <button type="button" onclick="setStmtYes(this)"
                class="stmt-yes px-3 py-1 text-sm rounded-lg border transition
                       @(Model.CorrectOptionIndices.Contains(i) ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">Yes</button>
        <button type="button" onclick="setStmtNo(this)"
                class="stmt-no px-3 py-1 text-sm rounded-lg border transition
                       @(!Model.CorrectOptionIndices.Contains(i) ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">No</button>
    </div>
    <button type="button" onclick="removeOption(this)"
            class="text-arcade-muted hover:text-red-400 transition flex-shrink-0 text-xl leading-none px-1">×</button>
</div>
```

- [ ] **Step 3: Update the options label and `switchQuestionType` JS**

Update the `options-label` span to include DragAndDrop text, and update `switchQuestionType` to handle the new type. In the `@section Scripts` block, update `switchQuestionType`:

```javascript
function switchQuestionType(value) {
    const isDnD = value === '@((int)QuestionType.DragAndDrop)';
    const isStatement = value === '@((int)QuestionType.Statement)';
    document.getElementById('allow-multiple-section').classList.toggle('hidden', isStatement || isDnD);
    const scoringSection = document.getElementById('scoring-mode-section');
    if (isStatement || isDnD) {
        scoringSection.classList.remove('hidden');
    } else {
        scoringSection.classList.toggle('hidden', !document.getElementById('AllowMultipleAnswers').checked);
    }
    document.querySelectorAll('.mc-only').forEach(el => el.classList.toggle('hidden', isStatement || isDnD));
    document.querySelectorAll('.stmt-only').forEach(el => el.classList.toggle('hidden', !isStatement));
    document.querySelectorAll('.dnd-only').forEach(el => el.classList.toggle('hidden', !isDnD));
    document.getElementById('options-label').textContent = isStatement
        ? 'Statements (correct answer per row)'
        : isDnD
        ? 'Pairs (label → sentence)'
        : 'Answer Options (mark the correct one(s))';
    document.getElementById('add-option-btn').textContent = isStatement
        ? '+ Add statement'
        : isDnD
        ? '+ Add pair'
        : '+ Add option';
    document.querySelectorAll('.option-text').forEach((input, i) => {
        input.placeholder = isStatement ? 'Statement ' + (i + 1)
            : isDnD ? 'Sentence ' + (i + 1)
            : 'Option ' + (i + 1);
    });
    document.querySelectorAll('.match-text').forEach((input, i) => {
        input.placeholder = 'Label ' + (i + 1);
    });
}
```

- [ ] **Step 4: Update `addOption` JS to include the MatchText input**

Replace the `addOption` function:

```javascript
function addOption() {
    const container = document.getElementById('options-container');
    const i = container.querySelectorAll('.option-row').length;
    const typeVal = document.querySelector('input[name="QuestionType"]:checked')?.value;
    const isStatement = typeVal === '@((int)QuestionType.Statement)';
    const isDnD = typeVal === '@((int)QuestionType.DragAndDrop)';
    const div = document.createElement('div');
    div.className = 'option-row flex items-center gap-3';
    div.innerHTML = `
        <input type="checkbox" name="CorrectOptionIndices" value="${i}"
               class="correct-input accent-yellow-500 w-4 h-4 flex-shrink-0 mc-only${(isStatement || isDnD) ? ' hidden' : ''}" />
        <input type="text" name="MatchTexts[${i}]" placeholder="Label ${i + 1}"
               class="match-text flex-shrink-0 w-32 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire dnd-only${isDnD ? '' : ' hidden'}" />
        <input type="text" name="OptionTexts[${i}]" placeholder="${isStatement ? 'Statement' : isDnD ? 'Sentence' : 'Option'} ${i + 1}"
               class="option-text flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
        <div class="stmt-only flex gap-1${isStatement ? '' : ' hidden'}">
            <button type="button" onclick="setStmtYes(this)"
                    class="stmt-yes px-3 py-1 text-sm rounded-lg border border-arcade-border text-arcade-muted transition">Yes</button>
            <button type="button" onclick="setStmtNo(this)"
                    class="stmt-no px-3 py-1 text-sm rounded-lg border border-fire text-fire transition">No</button>
        </div>
        <button type="button" onclick="removeOption(this)"
                class="text-arcade-muted hover:text-red-400 transition flex-shrink-0 text-xl leading-none px-1">×</button>
    `;
    container.appendChild(div);
}
```

- [ ] **Step 5: Update `reindexOptions` JS to re-number MatchText inputs too**

Replace `reindexOptions`:

```javascript
function reindexOptions() {
    const typeVal = document.querySelector('input[name="QuestionType"]:checked')?.value;
    const isStatement = typeVal === '@((int)QuestionType.Statement)';
    const isDnD = typeVal === '@((int)QuestionType.DragAndDrop)';
    document.querySelectorAll('#options-container .option-row').forEach((row, i) => {
        row.querySelector('.correct-input').value = i;
        const text = row.querySelector('.option-text');
        text.name = `OptionTexts[${i}]`;
        text.placeholder = isStatement ? 'Statement ' + (i + 1) : isDnD ? 'Sentence ' + (i + 1) : 'Option ' + (i + 1);
        const match = row.querySelector('.match-text');
        if (match) {
            match.name = `MatchTexts[${i}]`;
            match.placeholder = 'Label ' + (i + 1);
        }
    });
}
```

- [ ] **Step 6: Update the options label initial render**

The `options-label` is currently hardcoded to check for Statement. Update it to also handle DragAndDrop. Change the label text expression:

```cshtml
<label id="options-label" class="block text-sm font-bold text-arcade-muted mb-3">
    @(Model.QuestionType == QuestionType.Statement ? "Statements (correct answer per row)"
      : Model.QuestionType == QuestionType.DragAndDrop ? "Pairs (label → sentence)"
      : "Answer Options (mark the correct one(s))")
</label>
```

And the add-option button:

```cshtml
<button id="add-option-btn" type="button" onclick="addOption()"
        class="mt-3 px-4 py-2 text-sm border border-arcade-border rounded-lg text-arcade-muted hover:border-fire hover:text-fire transition">
    @(Model.QuestionType == QuestionType.Statement ? "+ Add statement"
      : Model.QuestionType == QuestionType.DragAndDrop ? "+ Add pair"
      : "+ Add option")
</button>
```

- [ ] **Step 7: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git commit -m "feat: add Drag & Drop question type to manager form with pairs UI"
```

---

## Task 11: Manual smoke test

- [ ] **Step 1: Start the app**

```bash
dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj
```

- [ ] **Step 2: Create a Drag & Drop question**

1. Log in, go to Manage → pick an exam → Add Question
2. Select "Drag & Drop" radio — confirm: pairs UI appears, scoring mode visible, "Allow multiple answers" hidden
3. Enter 3 pairs:
   - Label: `Paris` / Sentence: `Capital of France`
   - Label: `Berlin` / Sentence: `Capital of Germany`
   - Label: `London` / Sentence: `Capital of England`
4. Set scoring mode to Partial Credit
5. Save — confirm question appears in list

- [ ] **Step 3: Take the exam (Direct feedback mode)**

1. Start a session with Direct feedback
2. Reach the DragAndDrop question — confirm: pool of 3 shuffled chips, 3 drop zones
3. Drag `Paris` onto "Capital of France" — confirm: chip moves, slot fills
4. Drag `Berlin` onto "Capital of Italy" (wrong) — confirm: chip moves
5. Drag `London` onto "Capital of England" — confirm: all 3 slots filled, Submit enabled
6. Submit — confirm: green ✓ for Paris and London, red ✗ for Berlin with correct label shown
7. Continue to result page — confirm: per-slot breakdown shown

- [ ] **Step 4: Test touch fallback (use browser DevTools device mode or a phone)**

1. Tap a chip — confirm: it glows (ring-2 ring-white)
2. Tap a slot — confirm: chip moves there, glow removed
3. Tap a placed chip — confirm: it returns to pool
4. Fill all slots — confirm: Submit button enables

- [ ] **Step 5: Test AtEnd feedback + Review page**

1. Start a new session with AtEnd feedback
2. Answer the DragAndDrop question (mix correct/wrong)
3. On Review page — confirm: summary shows `Paris → Capital of France, …`
4. Click the question to revisit — confirm: slots pre-filled with previous answers
5. Change one answer, resubmit
6. Submit exam — confirm result page shows updated per-slot results

- [ ] **Step 6: Run all tests one final time**

```bash
dotnet test src/Examageddon.Tests/
```

Expected: all pass.
