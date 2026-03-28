# Statement Question Type Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Statement question type where each question has multiple statements and students answer Yes or No per statement.

**Architecture:** Add `Statement = 1` to the `QuestionType` enum; reuse `AnswerOption.IsCorrect` (true = Yes is correct), `SessionAnswerSelection`, and the existing `SubmitAnswerAsync` scoring logic unchanged. New UI branches in the manager form and the session answer partial handle the distinct Yes/No interaction.

**Tech Stack:** .NET 10, EF Core (SQLite, EnsureCreated), Razor Pages, HTMX, XUnit, Tailwind via CDN

---

## File Map

| File | Change |
|---|---|
| `src/Examageddon.Data/Enums/QuestionType.cs` | Add `Statement = 1` |
| `src/Examageddon.Services/Models/SessionQuestionModel.cs` | Add `QuestionType` property |
| `src/Examageddon.Services/ExamSessionService.cs` | Populate `QuestionType` in `GetSessionQuestionAsync` |
| `src/Examageddon.Tests/ExamSessionServiceTests.cs` | Two new tests for `QuestionType` in session model |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs` | Add `QuestionType` bind property, update validation, set on save |
| `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` | Question type selector, Statement option row UI, updated JS |
| `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml` | Statement unanswered + answered branches |

---

## Task 1: Enum, Service Layer, and Tests

**Files:**
- Modify: `src/Examageddon.Data/Enums/QuestionType.cs`
- Modify: `src/Examageddon.Services/Models/SessionQuestionModel.cs`
- Modify: `src/Examageddon.Services/ExamSessionService.cs`
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 1: Write two failing tests**

Add these two tests to `ExamSessionServiceTests.cs` before the `SeedAsync` helper (around line 375):

```csharp
[Fact]
public async Task GetSessionQuestion_ReturnsMultipleChoiceQuestionType()
{
    var (ctx, person, exam) = await SeedAsync(1);
    var svc = BuildService(ctx);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

    var model = await svc.GetSessionQuestionAsync(session.Id, 1);

    Assert.NotNull(model);
    Assert.Equal(QuestionType.MultipleChoice, model!.QuestionType);
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
    Assert.Equal(QuestionType.Statement, model!.QuestionType);
}
```

- [ ] **Step 2: Run tests — verify they fail**

```
dotnet test src/Examageddon.Tests/ --filter "GetSessionQuestion_Returns"
```

Expected: FAIL — `QuestionType` does not exist on `SessionQuestionModel` (compile error) or `Statement` does not exist on `QuestionType`.

- [ ] **Step 3: Add `Statement = 1` to the enum**

Replace the full contents of `src/Examageddon.Data/Enums/QuestionType.cs`:

```csharp
namespace Examageddon.Data.Enums;

public enum QuestionType
{
    MultipleChoice = 0,
    Statement = 1,
}
```

- [ ] **Step 4: Add `QuestionType` property to `SessionQuestionModel`**

In `src/Examageddon.Services/Models/SessionQuestionModel.cs`, add one property after `AllowMultipleAnswers`:

```csharp
public bool AllowMultipleAnswers { get; set; }

public QuestionType QuestionType { get; set; }
```

- [ ] **Step 5: Populate `QuestionType` in `GetSessionQuestionAsync`**

In `src/Examageddon.Services/ExamSessionService.cs`, inside `GetSessionQuestionAsync`, add one line to the `SessionQuestionModel` initializer:

```csharp
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
    QuestionType = sq.Question.QuestionType,
    IsFromReview = fromReview,
};
```

- [ ] **Step 6: Run tests — verify both new tests pass**

```
dotnet test src/Examageddon.Tests/ --filter "GetSessionQuestion_Returns"
```

Expected: PASS — both tests pass.

- [ ] **Step 7: Run full test suite**

```
dotnet test src/Examageddon.Tests/
```

Expected: All 31 existing tests + 2 new = **33 passed, 0 failed**.

- [ ] **Step 8: Commit**

```
git add src/Examageddon.Data/Enums/QuestionType.cs
git add src/Examageddon.Services/Models/SessionQuestionModel.cs
git add src/Examageddon.Services/ExamSessionService.cs
git add src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "feat: add Statement question type enum and QuestionType to session model"
```

---

## Task 2: Manager UI — Question Type Selector

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs`
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`

No unit tests — UI is manually tested by creating and editing questions.

- [ ] **Step 1: Update `QuestionForm.cshtml.cs`**

Replace the full contents of `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs`:

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
    public QuestionType QuestionType { get; set; }

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
            QuestionType = ExistingQuestion.QuestionType;
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

        if (QuestionType == QuestionType.Statement)
        {
            AllowMultipleAnswers = true;
            if (options.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one statement is required.");
                ExamId = id;
                return Page();
            }
        }
        else if (!options.Any(o => o.IsCorrect))
        {
            ModelState.AddModelError(string.Empty, "At least one answer option must be marked as correct.");
            ExamId = id;
            return Page();
        }

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
            existing.QuestionType = QuestionType;
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
                QuestionType = QuestionType,
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

Replace the full contents of `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`:

```razor
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

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-3">Question Type</label>
        <div class="flex gap-6">
            <label class="flex items-center gap-2 cursor-pointer">
                <input type="radio" name="QuestionType" value="@((int)QuestionType.MultipleChoice)"
                       @(Model.QuestionType == QuestionType.MultipleChoice ? "checked" : "")
                       onchange="switchQuestionType(this.value)"
                       class="accent-yellow-500" />
                <span class="text-sm text-arcade-text">Multiple Choice</span>
            </label>
            <label class="flex items-center gap-2 cursor-pointer">
                <input type="radio" name="QuestionType" value="@((int)QuestionType.Statement)"
                       @(Model.QuestionType == QuestionType.Statement ? "checked" : "")
                       onchange="switchQuestionType(this.value)"
                       class="accent-yellow-500" />
                <span class="text-sm text-arcade-text">Statement (Yes / No)</span>
            </label>
        </div>
    </div>

    <div id="allow-multiple-section" class="@(Model.QuestionType == QuestionType.Statement ? "hidden" : "") flex items-center gap-3">
        <input type="checkbox" asp-for="AllowMultipleAnswers" id="allowMultiple"
               class="accent-yellow-500 w-4 h-4"
               onchange="toggleScoringMode(this)" />
        <label for="allowMultiple" class="text-sm font-bold text-arcade-muted cursor-pointer">
            Allow multiple correct answers
        </label>
    </div>

    <div id="scoring-mode-section" class="@(Model.QuestionType == QuestionType.Statement || Model.AllowMultipleAnswers ? "" : "hidden") pl-7 space-y-2">
        <p class="text-sm font-bold text-arcade-muted mb-2">Scoring Mode</p>
        <label class="flex items-center gap-2 cursor-pointer">
            <input type="radio" name="ScoringMode" value="@((int)MultiAnswerScoringMode.AllOrNothing)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing ? "checked" : "")
                   class="accent-yellow-500" />
            <span class="text-sm text-arcade-text">All or nothing — must answer every statement correctly</span>
        </label>
        <label class="flex items-center gap-2 cursor-pointer">
            <input type="radio" name="ScoringMode" value="@((int)MultiAnswerScoringMode.PartialCredit)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.PartialCredit ? "checked" : "")
                   class="accent-yellow-500" />
            <span class="text-sm text-arcade-text">Partial credit — score per correct statement</span>
        </label>
    </div>

    <div>
        <label id="options-label" class="block text-sm font-bold text-arcade-muted mb-3">
            @(Model.QuestionType == QuestionType.Statement ? "Statements (correct answer per row)" : "Answer Options (mark the correct one(s))")
        </label>
        <div id="options-container" class="space-y-3">
            @for (int i = 0; i < Model.OptionTexts.Count; i++)
            {
                var isStmt = Model.QuestionType == QuestionType.Statement;
                <div class="option-row flex items-center gap-3">
                    <input type="checkbox" name="CorrectOptionIndices" value="@i"
                           @(Model.CorrectOptionIndices.Contains(i) ? "checked" : "")
                           class="correct-input accent-yellow-500 w-4 h-4 flex-shrink-0 mc-only @(isStmt ? "hidden" : "")" />
                    <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
                           placeholder="@(isStmt ? $"Statement {i + 1}" : $"Option {i + 1}")"
                           class="option-text flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
                    <div class="stmt-only @(isStmt ? "" : "hidden") flex gap-1">
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
            }
        </div>
        <button id="add-option-btn" type="button" onclick="addOption()"
                class="mt-3 px-4 py-2 text-sm border border-arcade-border rounded-lg text-arcade-muted hover:border-fire hover:text-fire transition">
            @(Model.QuestionType == QuestionType.Statement ? "+ Add statement" : "+ Add option")
        </button>
    </div>

    <div asp-validation-summary="All" class="text-red-400 text-sm"></div>

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

function switchQuestionType(value) {
    const isStatement = value === '@((int)QuestionType.Statement)';
    document.getElementById('allow-multiple-section').classList.toggle('hidden', isStatement);
    const scoringSection = document.getElementById('scoring-mode-section');
    if (isStatement) {
        scoringSection.classList.remove('hidden');
    } else {
        scoringSection.classList.toggle('hidden', !document.getElementById('AllowMultipleAnswers').checked);
    }
    document.querySelectorAll('.mc-only').forEach(el => el.classList.toggle('hidden', isStatement));
    document.querySelectorAll('.stmt-only').forEach(el => el.classList.toggle('hidden', !isStatement));
    document.getElementById('options-label').textContent = isStatement
        ? 'Statements (correct answer per row)'
        : 'Answer Options (mark the correct one(s))';
    document.getElementById('add-option-btn').textContent = isStatement ? '+ Add statement' : '+ Add option';
    document.querySelectorAll('.option-text').forEach((input, i) => {
        input.placeholder = (isStatement ? 'Statement' : 'Option') + ' ' + (i + 1);
    });
}

function reindexOptions() {
    const isStatement = document.querySelector('input[name="QuestionType"]:checked')?.value === '@((int)QuestionType.Statement)';
    document.querySelectorAll('#options-container .option-row').forEach((row, i) => {
        row.querySelector('.correct-input').value = i;
        const text = row.querySelector('input[type="text"]');
        text.name = `OptionTexts[${i}]`;
        text.placeholder = (isStatement ? 'Statement' : 'Option') + ' ' + (i + 1);
    });
}

function addOption() {
    const container = document.getElementById('options-container');
    const i = container.querySelectorAll('.option-row').length;
    const isStatement = document.querySelector('input[name="QuestionType"]:checked')?.value === '@((int)QuestionType.Statement)';
    const div = document.createElement('div');
    div.className = 'option-row flex items-center gap-3';
    div.innerHTML = `
        <input type="checkbox" name="CorrectOptionIndices" value="${i}"
               class="correct-input accent-yellow-500 w-4 h-4 flex-shrink-0 mc-only${isStatement ? ' hidden' : ''}" />
        <input type="text" name="OptionTexts[${i}]" placeholder="${isStatement ? 'Statement' : 'Option'} ${i + 1}"
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

function removeOption(btn) {
    btn.closest('.option-row').remove();
    reindexOptions();
}

function setStmtYes(btn) {
    const row = btn.closest('.option-row');
    row.querySelector('.correct-input').checked = true;
    row.querySelector('.stmt-yes').classList.add('border-fire', 'text-fire');
    row.querySelector('.stmt-yes').classList.remove('border-arcade-border', 'text-arcade-muted');
    row.querySelector('.stmt-no').classList.remove('border-fire', 'text-fire');
    row.querySelector('.stmt-no').classList.add('border-arcade-border', 'text-arcade-muted');
}

function setStmtNo(btn) {
    const row = btn.closest('.option-row');
    row.querySelector('.correct-input').checked = false;
    row.querySelector('.stmt-no').classList.add('border-fire', 'text-fire');
    row.querySelector('.stmt-no').classList.remove('border-arcade-border', 'text-arcade-muted');
    row.querySelector('.stmt-yes').classList.remove('border-fire', 'text-fire');
    row.querySelector('.stmt-yes').classList.add('border-arcade-border', 'text-arcade-muted');
}
</script>
}
```

- [ ] **Step 3: Build and verify no errors**

```
dotnet build src/Examageddon.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manual test — create a Statement question**

Start the app: `dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj`

1. Go to Manager → an exam → Add Question
2. Select "Statement (Yes / No)" — verify AllowMultipleAnswers hides, ScoringMode appears, option rows switch to Yes/No buttons
3. Type a question text, add 3 statements, set each to Yes or No
4. Save — verify question appears in the question list
5. Edit the question — verify type selector, scoring mode, and Yes/No selections are pre-populated correctly

- [ ] **Step 5: Commit**

```
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git commit -m "feat: add Statement question type to manager form"
```

---

## Task 3: Session UI — Statement Answer Partial

**Files:**
- Modify: `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`

No unit tests — UI tested manually in a session.

- [ ] **Step 1: Update `_AnswerFeedback.cshtml`**

Replace the full contents of `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`:

```razor
@model Examageddon.Services.Models.SessionQuestionModel
@using Examageddon.Data.Enums

@if (Model.QuestionType == QuestionType.Statement)
{
    @if (!Model.IsAnswered)
    {
        <div class="space-y-3">
            <form id="statement-form" class="space-y-3">
                @foreach (var option in Model.AnswerOptions)
                {
                    <input type="checkbox" id="sc_@option.Id" name="answerIds" value="@option.Id" class="hidden" />
                    <div class="flex items-center gap-3 border border-arcade-border rounded-xl p-4">
                        <span class="flex-1 text-arcade-text font-medium">@option.Text</span>
                        <button type="button" onclick="pickStmt('@option.Id', true)" id="yes_@option.Id"
                                class="px-4 py-1 rounded-lg border border-arcade-border text-arcade-muted text-sm font-medium transition hover:border-fire hover:text-fire">
                            Yes
                        </button>
                        <button type="button" onclick="pickStmt('@option.Id', false)" id="no_@option.Id"
                                class="px-4 py-1 rounded-lg border border-arcade-border text-arcade-muted text-sm font-medium transition hover:border-fire hover:text-fire">
                            No
                        </button>
                    </div>
                }
            </form>
            <button type="button" id="stmt-submit" disabled
                    hx-post="/sessions/@Model.SessionId/question/@Model.Position?handler=Answer&questionId=@Model.Question.Id"
                    hx-include="#statement-form"
                    hx-target="#answer-container"
                    hx-swap="innerHTML"
                    class="mt-4 w-full px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl opacity-50 cursor-not-allowed transition">
                Submit Answer
            </button>
        </div>
        <script>
        var _stmtAnswered = 0;
        var _stmtTotal = @Model.AnswerOptions.Count;
        function pickStmt(id, isYes) {
            var yBtn = document.getElementById('yes_' + id);
            var nBtn = document.getElementById('no_' + id);
            var wasAnswered = yBtn.classList.contains('border-fire') || nBtn.classList.contains('border-fire');
            if (!wasAnswered) _stmtAnswered++;
            document.getElementById('sc_' + id).checked = isYes;
            [yBtn, nBtn].forEach(function(b) {
                b.classList.remove('border-fire', 'text-fire');
                b.classList.add('border-arcade-border', 'text-arcade-muted');
            });
            var active = isYes ? yBtn : nBtn;
            active.classList.add('border-fire', 'text-fire');
            active.classList.remove('border-arcade-border', 'text-arcade-muted');
            if (_stmtAnswered >= _stmtTotal) {
                var sub = document.getElementById('stmt-submit');
                sub.disabled = false;
                sub.classList.remove('opacity-50', 'cursor-not-allowed');
            }
        }
        </script>
    }
    else
    {
        @{
            var showStmtFeedback = Model.FeedbackMode == FeedbackMode.Direct;
        }
        <div class="space-y-3">
            @foreach (var option in Model.AnswerOptions)
            {
                var userSaidYes = Model.SelectedAnswerOptionIds.Contains(option.Id);
                var isCorrectAnswer = userSaidYes == option.IsCorrect;
                string borderClass, bgClass, textClass;
                if (showStmtFeedback)
                {
                    if (isCorrectAnswer)
                    { borderClass = "border-green-500"; bgClass = "bg-green-500/10"; textClass = "text-green-400"; }
                    else
                    { borderClass = "border-red-500"; bgClass = "bg-red-500/10"; textClass = "text-red-400"; }
                }
                else
                { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-text"; }

                <div class="flex items-center gap-3 border @borderClass @bgClass rounded-xl p-4">
                    @if (showStmtFeedback)
                    {
                        if (isCorrectAnswer) { <span class="text-green-400 flex-shrink-0">✓</span> }
                        else { <span class="text-red-400 flex-shrink-0">✗</span> }
                    }
                    <span class="flex-1 @textClass font-medium">@option.Text</span>
                    <span class="px-4 py-1 rounded-lg border @(userSaidYes ? "border-fire text-fire" : "border-arcade-border text-arcade-muted") text-sm font-medium flex-shrink-0">
                        @(userSaidYes ? "Yes" : "No")
                    </span>
                </div>
            }
        </div>
    }
}
else if (!Model.AllowMultipleAnswers)
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

- [ ] **Step 2: Build and verify no errors**

```
dotnet build src/Examageddon.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run all tests**

```
dotnet test src/Examageddon.Tests/
```

Expected: **33 passed, 0 failed**.

- [ ] **Step 4: Manual test — answer a Statement question in a session**

1. Start the app: `dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj`
2. Ensure an exam has a Statement question (created in Task 2)
3. Start a session with Direct feedback mode
4. Navigate to the Statement question — verify each statement shows Yes/No buttons, Submit is disabled
5. Click Yes or No for each statement — verify Submit enables only after all are answered
6. Submit — verify feedback (green = correct, red = wrong)
7. Start a session with AtEnd feedback mode
8. Navigate to the Statement question — answer it — verify no colours until Review
9. Complete the session — check result page shows correct/wrong per statement

- [ ] **Step 5: Commit**

```
git add src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml
git commit -m "feat: add Statement question session UI with Yes/No per statement"
```
