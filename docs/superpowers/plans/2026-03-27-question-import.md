# Question JSON Import — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a JSON question import flow to the Manage area — upload a JSON file, stage questions in session for review/editing, then confirm to write to the database.

**Architecture:** Pure-logic `QuestionImportService` (Services project) handles parsing + validation + template generation. A static `ImportSession` helper (Web project) wraps session read/write. Two new Manage pages — Import (preview list) and ImportEdit (session-backed QuestionForm clone) — complete the flow. The Questions page gains a template download link and an upload form.

**Tech Stack:** .NET 10, Razor Pages, System.Text.Json, ASP.NET Core Session, xUnit

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `src/Examageddon.Services/Models/StagedQuestion.cs` | `StagedQuestion` + `StagedOption` DTOs |
| Create | `src/Examageddon.Services/QuestionImportService.cs` | Parse/validate JSON, build template, convert to `Question` entities |
| Create | `src/Examageddon.Tests/QuestionImportServiceTests.cs` | Unit tests for the service |
| Modify | `src/Examageddon.Web/Program.cs` | Register `QuestionImportService` as scoped |
| Modify | `src/Examageddon.Web/Pages/Manage/Questions.cshtml` | Template download link + upload form |
| Modify | `src/Examageddon.Web/Pages/Manage/Questions.cshtml.cs` | `OnGetTemplate` + `OnPostUploadAsync` handlers |
| Create | `src/Examageddon.Web/Pages/Manage/ImportSession.cs` | Static session read/write/clear helper |
| Create | `src/Examageddon.Web/Pages/Manage/Import.cshtml` | Preview list page |
| Create | `src/Examageddon.Web/Pages/Manage/Import.cshtml.cs` | Preview page model (Get, Remove, Cancel, Confirm) |
| Create | `src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml` | Session-backed question editor (QuestionForm clone, no image) |
| Create | `src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml.cs` | ImportEdit page model |

---

## Task 1: Service Layer — DTOs, QuestionImportService, Tests

**Files:**
- Create: `src/Examageddon.Services/Models/StagedQuestion.cs`
- Create: `src/Examageddon.Services/QuestionImportService.cs`
- Create: `src/Examageddon.Tests/QuestionImportServiceTests.cs`

- [ ] **Step 1: Create the DTOs**

`src/Examageddon.Services/Models/StagedQuestion.cs`:
```csharp
using Examageddon.Data.Enums;

namespace Examageddon.Services.Models;

public class StagedQuestion
{
    public QuestionType QuestionType { get; set; }
    public string Text { get; set; } = "";
    public bool AllowMultipleAnswers { get; set; }
    public MultiAnswerScoringMode ScoringMode { get; set; }
    public List<StagedOption> Options { get; set; } = [];
}

public class StagedOption
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string? MatchText { get; set; }  // label for DragAndDrop pairs
}
```

- [ ] **Step 2: Write the failing tests**

`src/Examageddon.Tests/QuestionImportServiceTests.cs`:
```csharp
using Examageddon.Data.Enums;
using Examageddon.Services;

namespace Examageddon.Tests;

public class QuestionImportServiceTests
{
    private readonly QuestionImportService _svc = new();

    // --- ParseAndValidate: success cases ---

    [Fact]
    public void ParseAndValidate_ValidMultipleChoice_ReturnsStagedQuestion()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q1","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":false}]}]
            """;

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        Assert.Single(questions);
        Assert.Equal(QuestionType.MultipleChoice, questions[0].QuestionType);
        Assert.Equal("Q1", questions[0].Text);
        Assert.Equal(2, questions[0].Options.Count);
        Assert.True(questions[0].Options[0].IsCorrect);
        Assert.False(questions[0].Options[1].IsCorrect);
    }

    [Fact]
    public void ParseAndValidate_ValidDragAndDrop_NormalisesToStagedOptions()
    {
        var json = """
            [{"type":"DragAndDrop","text":"Match","pairs":[
              {"label":"Blob","sentence":"Stores objects"}]}]
            """;

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        var q = questions![0];
        Assert.Equal(QuestionType.DragAndDrop, q.QuestionType);
        Assert.Single(q.Options);
        Assert.Equal("Blob", q.Options[0].MatchText);
        Assert.Equal("Stores objects", q.Options[0].Text);
    }

    [Fact]
    public void ParseAndValidate_ValidStatement_ReturnsStagedQuestion()
    {
        var json = """
            [{"type":"Statement","text":"Evaluate","options":[
              {"text":"Blob supports NFS","isCorrect":true}]}]
            """;

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        Assert.Equal(QuestionType.Statement, questions![0].QuestionType);
    }

    [Fact]
    public void ParseAndValidate_TypeIsCaseInsensitive()
    {
        var json = """
            [{"type":"multiplechoice","text":"Q","options":[{"text":"A","isCorrect":true}]}]
            """;

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.Equal(QuestionType.MultipleChoice, questions![0].QuestionType);
    }

    [Fact]
    public void ParseAndValidate_AllowMultipleAnswers_DefaultsFalse()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q","options":[{"text":"A","isCorrect":true}]}]
            """;

        var (questions, _) = _svc.ParseAndValidate(json);

        Assert.False(questions![0].AllowMultipleAnswers);
    }

    [Fact]
    public void ParseAndValidate_ScoringMode_DefaultsAllOrNothing()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q","allowMultipleAnswers":true,"options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (questions, _) = _svc.ParseAndValidate(json);

        Assert.Equal(MultiAnswerScoringMode.AllOrNothing, questions![0].ScoringMode);
    }

    [Fact]
    public void ParseAndValidate_ScoringModePartialCredit_Parsed()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q","allowMultipleAnswers":true,"scoringMode":"PartialCredit","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (questions, _) = _svc.ParseAndValidate(json);

        Assert.Equal(MultiAnswerScoringMode.PartialCredit, questions![0].ScoringMode);
    }

    // --- ParseAndValidate: error cases ---

    [Fact]
    public void ParseAndValidate_InvalidJson_ReturnsError()
    {
        var (questions, errors) = _svc.ParseAndValidate("not json");

        Assert.Null(questions);
        Assert.Single(errors);
        Assert.Equal("Invalid JSON file", errors[0]);
    }

    [Fact]
    public void ParseAndValidate_RootNotArray_ReturnsError()
    {
        var (questions, errors) = _svc.ParseAndValidate("{\"type\":\"MultipleChoice\"}");

        Assert.Null(questions);
        Assert.Single(errors);
        Assert.Equal("File must contain a JSON array", errors[0]);
    }

    [Fact]
    public void ParseAndValidate_UnknownType_ReturnsError()
    {
        var json = """[{"type":"Bogus","text":"Q"}]""";

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Null(questions);
        Assert.Contains(errors, e => e == "Question 1: unknown type \"Bogus\"");
    }

    [Fact]
    public void ParseAndValidate_MissingText_ReturnsError()
    {
        var json = """[{"type":"MultipleChoice","options":[{"text":"A","isCorrect":true}]}]""";

        var (questions, errors) = _svc.ParseAndValidate(json);

        Assert.Null(questions);
        Assert.Contains(errors, e => e == "Question 1: text is required");
    }

    [Fact]
    public void ParseAndValidate_MC_NoOptions_ReturnsError()
    {
        var json = """[{"type":"MultipleChoice","text":"Q"}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one option is required");
    }

    [Fact]
    public void ParseAndValidate_MC_NoCorrectAnswer_ReturnsError()
    {
        var json = """[{"type":"MultipleChoice","text":"Q","options":[{"text":"A","isCorrect":false}]}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one correct answer is required");
    }

    [Fact]
    public void ParseAndValidate_MC_MultipleCorrectWithoutFlag_ReturnsError()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: multiple correct answers require allowMultipleAnswers: true");
    }

    [Fact]
    public void ParseAndValidate_Statement_NoOptions_ReturnsError()
    {
        var json = """[{"type":"Statement","text":"Q"}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one option is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_NoPairs_ReturnsError()
    {
        var json = """[{"type":"DragAndDrop","text":"Q"}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one pair is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_PairMissingLabel_ReturnsError()
    {
        var json = """[{"type":"DragAndDrop","text":"Q","pairs":[{"sentence":"S"}]}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1, pair 1: label is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_PairMissingSentence_ReturnsError()
    {
        var json = """[{"type":"DragAndDrop","text":"Q","pairs":[{"label":"L"}]}]""";

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1, pair 1: sentence is required");
    }

    [Fact]
    public void ParseAndValidate_ErrorsAreNumberedFrom1()
    {
        var json = """
            [{"type":"MultipleChoice","text":"Q1","options":[{"text":"A","isCorrect":true}]},
             {"type":"MultipleChoice","text":"Q2"}]
            """;

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.Contains(errors, e => e.StartsWith("Question 2:"));
    }

    [Fact]
    public void ParseAndValidate_AllErrorsCollected_NotStoppedAtFirst()
    {
        var json = """
            [{"type":"MultipleChoice","text":""},
             {"type":"DragAndDrop","text":""}]
            """;

        var (_, errors) = _svc.ParseAndValidate(json);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.StartsWith("Question 1:"));
        Assert.Contains(errors, e => e.StartsWith("Question 2:"));
    }

    // --- ToQuestion ---

    [Fact]
    public void ToQuestion_MC_MapsAllFields()
    {
        var staged = new Examageddon.Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.MultipleChoice,
            Text = "What is X?",
            AllowMultipleAnswers = false,
            ScoringMode = MultiAnswerScoringMode.AllOrNothing,
            Options =
            [
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = false },
            ]
        };

        var question = _svc.ToQuestion(staged, examId: 5, orderIndex: 2);

        Assert.Equal(5, question.ExamId);
        Assert.Equal("What is X?", question.Text);
        Assert.Equal(QuestionType.MultipleChoice, question.QuestionType);
        Assert.Equal(2, question.OrderIndex);
        Assert.Equal(2, question.AnswerOptions.Count);
        Assert.True(question.AnswerOptions.First(a => a.Text == "A").IsCorrect);
    }

    [Fact]
    public void ToQuestion_DnD_SetsAllowMultipleAnswersTrue()
    {
        var staged = new Examageddon.Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.DragAndDrop,
            Text = "Match",
            Options = [new() { Text = "S", MatchText = "L", IsCorrect = true }]
        };

        var question = _svc.ToQuestion(staged, examId: 1, orderIndex: 0);

        Assert.True(question.AllowMultipleAnswers);
        Assert.Equal("L", question.AnswerOptions[0].MatchText);
    }

    [Fact]
    public void ToQuestion_Statement_SetsAllowMultipleAnswersTrue()
    {
        var staged = new Examageddon.Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.Statement,
            Text = "Evaluate",
            Options = [new() { Text = "Blob supports NFS", IsCorrect = true }]
        };

        var question = _svc.ToQuestion(staged, examId: 1, orderIndex: 0);

        Assert.True(question.AllowMultipleAnswers);
    }

    // --- GetTemplateJson ---

    [Fact]
    public void GetTemplateJson_ReturnsValidJsonArray()
    {
        var json = _svc.GetTemplateJson();

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 3);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```
dotnet test src/Examageddon.Tests/ --filter "QuestionImportServiceTests"
```

Expected: compile error — `QuestionImportService` does not exist.

- [ ] **Step 4: Implement QuestionImportService**

`src/Examageddon.Services/QuestionImportService.cs`:
```csharp
using System.Text.Json;
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services.Models;

namespace Examageddon.Services;

public class QuestionImportService
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public (List<StagedQuestion>? Questions, List<string> Errors) ParseAndValidate(string json)
    {
        var errors = new List<string>();
        List<JsonElement> items;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (null, ["File must contain a JSON array"]);
            items = [.. doc.RootElement.EnumerateArray()];
        }
        catch (JsonException)
        {
            return (null, ["Invalid JSON file"]);
        }

        var questions = new List<StagedQuestion>();

        for (int n = 0; n < items.Count; n++)
        {
            var q = items[n];
            int num = n + 1;

            string? typeStr = q.TryGetProperty("type", out var tp) ? tp.GetString() : null;
            QuestionType? questionType = typeStr?.ToLowerInvariant() switch
            {
                "multiplechoice" => QuestionType.MultipleChoice,
                "statement" => QuestionType.Statement,
                "draganddrop" => QuestionType.DragAndDrop,
                _ => null,
            };

            if (questionType is null)
            {
                errors.Add($"Question {num}: unknown type \"{typeStr ?? ""}\"");
                continue;
            }

            string? text = q.TryGetProperty("text", out var txtProp) ? txtProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(text))
                errors.Add($"Question {num}: text is required");

            bool allowMultiple = q.TryGetProperty("allowMultipleAnswers", out var amp) && amp.GetBoolean();

            string smStr = q.TryGetProperty("scoringMode", out var smp) ? smp.GetString() ?? "" : "";
            MultiAnswerScoringMode scoringMode = smStr.ToLowerInvariant() == "partialcredit"
                ? MultiAnswerScoringMode.PartialCredit
                : MultiAnswerScoringMode.AllOrNothing;

            var staged = new StagedQuestion
            {
                QuestionType = questionType.Value,
                Text = text ?? "",
                AllowMultipleAnswers = allowMultiple,
                ScoringMode = scoringMode,
            };

            if (questionType == QuestionType.DragAndDrop)
            {
                if (!q.TryGetProperty("pairs", out var pairsProp)
                    || pairsProp.ValueKind != JsonValueKind.Array
                    || pairsProp.GetArrayLength() == 0)
                {
                    errors.Add($"Question {num}: at least one pair is required");
                }
                else
                {
                    var pairItems = pairsProp.EnumerateArray().ToList();
                    for (int m = 0; m < pairItems.Count; m++)
                    {
                        var pair = pairItems[m];
                        int pNum = m + 1;
                        string? label = pair.TryGetProperty("label", out var lp) ? lp.GetString() : null;
                        string? sentence = pair.TryGetProperty("sentence", out var sp) ? sp.GetString() : null;

                        if (string.IsNullOrWhiteSpace(label))
                            errors.Add($"Question {num}, pair {pNum}: label is required");
                        if (string.IsNullOrWhiteSpace(sentence))
                            errors.Add($"Question {num}, pair {pNum}: sentence is required");

                        staged.Options.Add(new StagedOption
                        {
                            Text = sentence ?? "",
                            MatchText = label ?? "",
                            IsCorrect = true,
                        });
                    }
                }
            }
            else
            {
                if (!q.TryGetProperty("options", out var optsProp)
                    || optsProp.ValueKind != JsonValueKind.Array
                    || optsProp.GetArrayLength() == 0)
                {
                    errors.Add($"Question {num}: at least one option is required");
                }
                else
                {
                    var optItems = optsProp.EnumerateArray().ToList();
                    int correctCount = 0;

                    for (int m = 0; m < optItems.Count; m++)
                    {
                        var opt = optItems[m];
                        int oNum = m + 1;
                        string? optText = opt.TryGetProperty("text", out var otp) ? otp.GetString() : null;
                        bool isCorrect = opt.TryGetProperty("isCorrect", out var icp) && icp.GetBoolean();

                        if (questionType == QuestionType.Statement && string.IsNullOrWhiteSpace(optText))
                            errors.Add($"Question {num}, option {oNum}: text is required");

                        if (isCorrect) correctCount++;
                        staged.Options.Add(new StagedOption { Text = optText ?? "", IsCorrect = isCorrect });
                    }

                    if (questionType == QuestionType.MultipleChoice)
                    {
                        if (correctCount == 0)
                            errors.Add($"Question {num}: at least one correct answer is required");
                        if (correctCount > 1 && !allowMultiple)
                            errors.Add($"Question {num}: multiple correct answers require allowMultipleAnswers: true");
                    }
                }
            }

            questions.Add(staged);
        }

        return errors.Count > 0 ? (null, errors) : (questions, []);
    }

    public Question ToQuestion(StagedQuestion staged, int examId, int orderIndex)
    {
        bool forceMultiple = staged.QuestionType is QuestionType.DragAndDrop or QuestionType.Statement;

        var options = staged.Options.Select((opt, idx) => new AnswerOption
        {
            Text = opt.Text,
            MatchText = opt.MatchText,
            IsCorrect = staged.QuestionType == QuestionType.DragAndDrop
                ? !string.IsNullOrWhiteSpace(opt.Text) && !string.IsNullOrWhiteSpace(opt.MatchText)
                : opt.IsCorrect,
            OrderIndex = idx,
        }).ToList();

        return new Question
        {
            ExamId = examId,
            Text = staged.Text,
            QuestionType = staged.QuestionType,
            AllowMultipleAnswers = forceMultiple || staged.AllowMultipleAnswers,
            ScoringMode = staged.ScoringMode,
            OrderIndex = orderIndex,
            AnswerOptions = options,
        };
    }

    public string GetTemplateJson() => """
        [
          {
            "type": "MultipleChoice",
            "text": "What is the default retention period for Azure Monitor logs?",
            "allowMultipleAnswers": false,
            "scoringMode": "AllOrNothing",
            "options": [
              { "text": "30 days", "isCorrect": true },
              { "text": "7 days",  "isCorrect": false },
              { "text": "90 days", "isCorrect": false }
            ]
          },
          {
            "type": "MultipleChoice",
            "text": "Which services support availability zones?",
            "allowMultipleAnswers": true,
            "scoringMode": "PartialCredit",
            "options": [
              { "text": "Azure SQL Database", "isCorrect": true },
              { "text": "Azure VMs",          "isCorrect": true },
              { "text": "Azure DNS",          "isCorrect": false }
            ]
          },
          {
            "type": "Statement",
            "text": "Evaluate each statement about Azure Storage.",
            "options": [
              { "text": "Blob storage supports NFS 3.0",         "isCorrect": true },
              { "text": "Table storage is a relational database", "isCorrect": false }
            ]
          },
          {
            "type": "DragAndDrop",
            "text": "Match each Azure service to its primary purpose.",
            "pairs": [
              { "label": "Azure Blob Storage", "sentence": "Stores unstructured object data at scale" },
              { "label": "Azure Functions",    "sentence": "Runs event-driven serverless code" }
            ]
          }
        ]
        """;
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test src/Examageddon.Tests/ --filter "QuestionImportServiceTests"
```

Expected: all tests pass, 0 failures.

- [ ] **Step 6: Build solution**

```
dotnet build src/Examageddon.slnx
```

Expected: 0 errors.

---

## Task 2: Questions Page — Template Download + Upload + Service Registration

**Files:**
- Modify: `src/Examageddon.Web/Program.cs`
- Modify: `src/Examageddon.Web/Pages/Manage/Questions.cshtml`
- Modify: `src/Examageddon.Web/Pages/Manage/Questions.cshtml.cs`

- [ ] **Step 1: Register the service in Program.cs**

In `src/Examageddon.Web/Program.cs`, add after the existing `AddScoped<HistoryService>()` line:

```csharp
builder.Services.AddScoped<QuestionImportService>();
```

The using directive is not needed — `QuestionImportService` is in `Examageddon.Services` which is already referenced. The top-level using statement `using Examageddon.Services;` is already present via implicit usings or existing code.

- [ ] **Step 2: Update Questions.cshtml**

Replace the entire content of `src/Examageddon.Web/Pages/Manage/Questions.cshtml`:

```razor
@page "/manage/exams/{id:int}/questions"
@model Examageddon.Web.Pages.Manage.QuestionsModel
@{ ViewData["Title"] = $"Questions — {Model.Exam.Title}"; }

<div class="flex items-center justify-between mb-8">
    <div>
        <a href="/manage" class="text-arcade-muted text-sm hover:text-arcade-text">← Manage</a>
        <h1 class="text-3xl font-black mt-1">@Model.Exam.Title</h1>
        <p class="text-arcade-muted">@Model.Questions.Count questions</p>
    </div>
    <a href="/manage/exams/@Model.Exam.Id/questions/0/edit"
       class="px-5 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg hover:opacity-90 transition">
        + Add Question
    </a>
</div>

@* Import errors from upload validation *@
@if (TempData["ImportErrors"] is string errStr)
{
    <div class="bg-arcade-card border border-red-800 rounded-xl p-4 mb-4">
        <p class="text-xs font-bold tracking-widest text-red-400 uppercase mb-2">Import Errors</p>
        <ul class="space-y-1">
            @foreach (var err in errStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                <li class="text-sm text-red-300">@err</li>
            }
        </ul>
    </div>
}

@* Import tools card *@
<div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden mb-6">
    <div class="px-4 py-2.5 border-b border-arcade-border">
        <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Import Questions</span>
    </div>
    <div class="p-4 flex items-center gap-4">
        <a asp-page-handler="Template" asp-route-id="@Model.Exam.Id"
           class="px-4 py-2 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition">
            ↓ Download Template
        </a>
        <form method="post" asp-page-handler="Upload" asp-route-id="@Model.Exam.Id"
              enctype="multipart/form-data" class="flex items-center gap-3">
            @Html.AntiForgeryToken()
            <label class="px-4 py-2 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition cursor-pointer">
                <span id="file-name">Choose JSON file…</span>
                <input type="file" name="file" accept=".json,application/json" class="hidden"
                       onchange="document.getElementById('file-name').textContent = this.files[0]?.name ?? 'Choose JSON file…'" />
            </label>
            <button type="submit"
                    class="px-4 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg text-sm hover:opacity-90 transition">
                Import
            </button>
        </form>
    </div>
</div>

@if (!Model.Questions.Any())
{
    <div class="bg-arcade-card border border-arcade-border rounded-xl p-8 text-center text-arcade-muted">
        No questions yet. Add one!
    </div>
}
else
{
    <div class="space-y-3">
        @foreach (var (q, i) in Model.Questions.Select((q, i) => (q, i)))
        {
            <div class="bg-arcade-card border border-arcade-border rounded-xl p-5 flex items-center gap-4">
                <span class="text-arcade-muted font-bold w-6 text-center">@(i+1)</span>
                <div class="flex-1">
                    <p class="font-medium">@q.Text</p>
                    <p class="text-arcade-muted text-sm">@q.AnswerOptions.Count options · @(q.ImageData != null ? "📷 has image" : "")</p>
                </div>
                <div class="flex gap-3">
                    <a href="/manage/exams/@Model.Exam.Id/questions/@q.Id/edit"
                       class="px-3 py-1.5 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition">Edit</a>
                    <form method="post" asp-page-handler="Delete" asp-route-id="@Model.Exam.Id" asp-route-questionId="@q.Id"
                          onsubmit="return confirm('Delete this question?')">
                        <button type="submit" class="px-3 py-1.5 bg-arcade-dark border border-red-800 rounded-lg text-sm text-red-400 hover:border-red-500 transition">
                            Delete
                        </button>
                    </form>
                </div>
            </div>
        }
    </div>
}
```

- [ ] **Step 3: Update Questions.cshtml.cs**

Replace the entire content of `src/Examageddon.Web/Pages/Manage/Questions.cshtml.cs`:

```csharp
using System.Text;
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class QuestionsModel(ExamManagementService examService, QuestionImportService importService) : PageModel
{
    public Exam Exam { get; set; } = null!;

    public List<Question> Questions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var exam = await examService.GetExamAsync(id);
        if (exam is null)
            return NotFound();

        Exam = exam;
        Questions = await examService.GetQuestionsAsync(id);
        return Page();
    }

    public IActionResult OnGetTemplate(int id)
    {
        var json = importService.GetTemplateJson();
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "question-template.json");
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ImportErrors"] = "No file selected";
            return RedirectToPage(new { id });
        }

        string json;
        using (var reader = new StreamReader(file.OpenReadStream()))
            json = await reader.ReadToEndAsync();

        var (questions, errors) = importService.ParseAndValidate(json);

        if (errors.Count > 0)
        {
            TempData["ImportErrors"] = string.Join("\n", errors);
            return RedirectToPage(new { id });
        }

        ImportSession.Set(HttpContext.Session, id, questions!);
        return RedirectToPage("/Manage/Import", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int questionId)
    {
        await examService.DeleteQuestionAsync(questionId);
        return RedirectToPage(new { id });
    }
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build src/Examageddon.slnx
```

Expected: compile error — `ImportSession` does not exist yet. This is expected; it will be created in Task 3.

---

## Task 3: Session Helper + Import Preview Page

**Files:**
- Create: `src/Examageddon.Web/Pages/Manage/ImportSession.cs`
- Create: `src/Examageddon.Web/Pages/Manage/Import.cshtml`
- Create: `src/Examageddon.Web/Pages/Manage/Import.cshtml.cs`

- [ ] **Step 1: Create ImportSession helper**

`src/Examageddon.Web/Pages/Manage/ImportSession.cs`:
```csharp
using System.Text.Json;
using Examageddon.Services.Models;

namespace Examageddon.Web.Pages.Manage;

internal static class ImportSession
{
    private static string Key(int examId) => $"import_{examId}";

    public static List<StagedQuestion>? Get(ISession session, int examId)
    {
        var json = session.GetString(Key(examId));
        return json is null ? null : JsonSerializer.Deserialize<List<StagedQuestion>>(json);
    }

    public static void Set(ISession session, int examId, List<StagedQuestion> questions)
    {
        session.SetString(Key(examId), JsonSerializer.Serialize(questions));
    }

    public static void Clear(ISession session, int examId)
    {
        session.Remove(Key(examId));
    }
}
```

- [ ] **Step 2: Build to verify Questions.cshtml.cs compiles**

```
dotnet build src/Examageddon.slnx
```

Expected: 0 errors (Import.cshtml.cs doesn't exist yet but isn't referenced by anything that's been built).

- [ ] **Step 3: Create Import.cshtml.cs**

`src/Examageddon.Web/Pages/Manage/Import.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ImportModel(ExamManagementService examService, QuestionImportService importService) : PageModel
{
    public Exam Exam { get; set; } = null!;
    public List<StagedQuestion> StagedQuestions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null or { Count: 0 })
            return RedirectToPage("/Manage/Questions", new { id });

        var exam = await examService.GetExamAsync(id);
        if (exam is null)
            return NotFound();

        Exam = exam;
        StagedQuestions = staged;
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id, int index)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is not null && index >= 0 && index < staged.Count)
        {
            staged.RemoveAt(index);
            if (staged.Count == 0)
            {
                ImportSession.Clear(HttpContext.Session, id);
                return RedirectToPage("/Manage/Questions", new { id });
            }
            ImportSession.Set(HttpContext.Session, id, staged);
        }
        return RedirectToPage(new { id });
    }

    public IActionResult OnPostCancel(int id)
    {
        ImportSession.Clear(HttpContext.Session, id);
        return RedirectToPage("/Manage/Questions", new { id });
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null or { Count: 0 })
            return RedirectToPage("/Manage/Questions", new { id });

        var existing = await examService.GetQuestionsAsync(id);
        int baseIndex = existing.Count;

        for (int i = 0; i < staged.Count; i++)
        {
            var question = importService.ToQuestion(staged[i], id, baseIndex + i);
            await examService.AddQuestionAsync(question);
        }

        ImportSession.Clear(HttpContext.Session, id);
        return RedirectToPage("/Manage/Questions", new { id });
    }
}
```

- [ ] **Step 4: Create Import.cshtml**

`src/Examageddon.Web/Pages/Manage/Import.cshtml`:
```razor
@page "/manage/exams/{id:int}/import"
@model Examageddon.Web.Pages.Manage.ImportModel
@using Examageddon.Data.Enums
@{ ViewData["Title"] = "Import Preview"; }

<div class="mb-8">
    <a href="/manage/exams/@Model.Exam.Id/questions" class="text-arcade-muted text-sm hover:text-arcade-text">← @Model.Exam.Title</a>
    <div class="flex items-center justify-between mt-1">
        <h1 class="text-3xl font-black">Import Preview</h1>
        <span class="text-xs text-arcade-muted bg-arcade-card border border-arcade-border rounded-lg px-3 py-1">
            @Model.StagedQuestions.Count question@(Model.StagedQuestions.Count == 1 ? "" : "s")
        </span>
    </div>
    <p class="text-arcade-muted text-sm mt-1">Review and edit before importing. Nothing is saved until you click Import.</p>
</div>

<div class="space-y-4">

    @* Staged questions list *@
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border flex items-center justify-between">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Questions</span>
            <span class="text-xs text-arcade-muted">Click Edit to modify</span>
        </div>
        <div class="divide-y divide-arcade-border">
            @for (int i = 0; i < Model.StagedQuestions.Count; i++)
            {
                var q = Model.StagedQuestions[i];
                string typeIcon = q.QuestionType switch
                {
                    QuestionType.MultipleChoice => "☑",
                    QuestionType.Statement => "⊙",
                    QuestionType.DragAndDrop => "⇄",
                    _ => "?"
                };
                string typeLabel = q.QuestionType switch
                {
                    QuestionType.MultipleChoice => "Multiple Choice",
                    QuestionType.Statement => "Statement",
                    QuestionType.DragAndDrop => "Drag & Drop",
                    _ => ""
                };
                string meta = q.QuestionType switch
                {
                    QuestionType.MultipleChoice =>
                        $"Multiple Choice · {q.Options.Count} options" +
                        (q.AllowMultipleAnswers
                            ? $" · multiple correct · {(q.ScoringMode == Examageddon.Data.Enums.MultiAnswerScoringMode.PartialCredit ? "Partial Credit" : "All or Nothing")}"
                            : " · single correct"),
                    QuestionType.Statement =>
                        $"Statement · {q.Options.Count} statement{(q.Options.Count == 1 ? "" : "s")}",
                    QuestionType.DragAndDrop =>
                        $"Drag & Drop · {q.Options.Count} pair{(q.Options.Count == 1 ? "" : "s")}",
                    _ => ""
                };

                <div class="flex items-center gap-3 px-4 py-3">
                    <span class="text-sm text-arcade-muted font-bold w-5 text-center flex-shrink-0">@(i + 1)</span>
                    <span class="text-base flex-shrink-0" title="@typeLabel">@typeIcon</span>
                    <div class="flex-1 min-w-0">
                        <p class="text-sm font-medium truncate">@q.Text</p>
                        <p class="text-xs text-arcade-muted mt-0.5">@meta</p>
                    </div>
                    <div class="flex gap-2 flex-shrink-0">
                        <a href="/manage/exams/@Model.Exam.Id/import/@i/edit"
                           class="px-3 py-1 text-xs border border-arcade-border rounded-lg text-arcade-muted hover:border-fire transition bg-arcade-dark">Edit</a>
                        <form method="post" asp-page-handler="Remove" asp-route-id="@Model.Exam.Id" asp-route-index="@i">
                            @Html.AntiForgeryToken()
                            <button type="submit"
                                    class="px-3 py-1 text-xs border border-red-900 rounded-lg text-red-400 hover:border-red-500 transition bg-transparent">
                                Remove
                            </button>
                        </form>
                    </div>
                </div>
            }
        </div>
    </div>

    @* Confirm import *@
    <form method="post" asp-page-handler="Confirm" asp-route-id="@Model.Exam.Id">
        @Html.AntiForgeryToken()
        <button type="submit"
                class="w-full py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition text-base">
            Import @Model.StagedQuestions.Count Question@(Model.StagedQuestions.Count == 1 ? "" : "s")
        </button>
    </form>

    @* Cancel *@
    <form method="post" asp-page-handler="Cancel" asp-route-id="@Model.Exam.Id">
        @Html.AntiForgeryToken()
        <button type="submit"
                class="w-full py-2.5 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition text-sm text-arcade-muted">
            Cancel — back to Questions
        </button>
    </form>

</div>
```

- [ ] **Step 5: Build to verify**

```
dotnet build src/Examageddon.slnx
```

Expected: 0 errors.

---

## Task 4: ImportEdit Page

**Files:**
- Create: `src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml`
- Create: `src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml.cs`

- [ ] **Step 1: Create ImportEdit.cshtml.cs**

`src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml.cs`:
```csharp
using Examageddon.Data.Enums;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ImportEditModel : PageModel
{
    [BindProperty] public int ExamId { get; set; }
    [BindProperty] public int Index { get; set; }
    [BindProperty] public QuestionType QuestionType { get; set; }
    [BindProperty] public string QuestionText { get; set; } = string.Empty;
    [BindProperty] public bool AllowMultipleAnswers { get; set; }
    [BindProperty] public MultiAnswerScoringMode ScoringMode { get; set; }
    [BindProperty] public List<string> OptionTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];
    [BindProperty] public List<string> MatchTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];
    [BindProperty] public List<int> CorrectOptionIndices { get; set; } = [];

    public IActionResult OnGet(int id, int index)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null || index < 0 || index >= staged.Count)
            return RedirectToPage("/Manage/Import", new { id });

        var q = staged[index];
        ExamId = id;
        Index = index;
        QuestionType = q.QuestionType;
        QuestionText = q.Text;
        AllowMultipleAnswers = q.AllowMultipleAnswers;
        ScoringMode = q.ScoringMode;
        OptionTexts = [.. q.Options.Select(o => o.Text)];
        MatchTexts = [.. q.Options.Select(o => o.MatchText ?? string.Empty)];
        CorrectOptionIndices = [.. q.Options.Select((o, i) => (o, i)).Where(x => x.o.IsCorrect).Select(x => x.i)];
        return Page();
    }

    public IActionResult OnPost(int id, int index)
    {
        var options = OptionTexts
            .Select((text, idx) => new StagedOption
            {
                Text = text,
                MatchText = QuestionType == QuestionType.DragAndDrop && idx < MatchTexts.Count
                    ? MatchTexts[idx]
                    : null,
                IsCorrect = QuestionType == QuestionType.DragAndDrop
                    ? !string.IsNullOrWhiteSpace(text) && idx < MatchTexts.Count && !string.IsNullOrWhiteSpace(MatchTexts[idx])
                    : CorrectOptionIndices.Contains(idx),
            })
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();

        if (QuestionType == QuestionType.DragAndDrop)
        {
            if (options.Count == 0 || options.Any(o => string.IsNullOrWhiteSpace(o.MatchText)))
            {
                ModelState.AddModelError(string.Empty, "Each pair must have both a label and a sentence.");
                ExamId = id; Index = index;
                return Page();
            }
        }
        else if (QuestionType == QuestionType.Statement)
        {
            if (options.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one statement is required.");
                ExamId = id; Index = index;
                return Page();
            }
        }
        else if (!options.Any(o => o.IsCorrect))
        {
            ModelState.AddModelError(string.Empty, "At least one answer option must be marked as correct.");
            ExamId = id; Index = index;
            return Page();
        }

        bool forceMultiple = QuestionType is QuestionType.DragAndDrop or QuestionType.Statement;

        var updated = new StagedQuestion
        {
            QuestionType = QuestionType,
            Text = QuestionText,
            AllowMultipleAnswers = forceMultiple || AllowMultipleAnswers,
            ScoringMode = ScoringMode,
            Options = options,
        };

        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is not null && index >= 0 && index < staged.Count)
        {
            staged[index] = updated;
            ImportSession.Set(HttpContext.Session, id, staged);
        }

        return RedirectToPage("/Manage/Import", new { id });
    }
}
```

- [ ] **Step 2: Create ImportEdit.cshtml**

`src/Examageddon.Web/Pages/Manage/ImportEdit.cshtml`:
```razor
@page "/manage/exams/{id:int}/import/{index:int}/edit"
@model Examageddon.Web.Pages.Manage.ImportEditModel
@using Examageddon.Data.Enums
@{ ViewData["Title"] = "Edit Staged Question"; }

<h1 class="text-3xl font-black mb-8">Edit Question</h1>

<form method="post" class="space-y-4">
    <input type="hidden" asp-for="ExamId" />
    <input type="hidden" asp-for="Index" />

    <!-- Section: Question text -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Question</span>
        </div>
        <div class="p-4">
            <textarea asp-for="QuestionText" rows="3" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire resize-y"></textarea>
        </div>
    </div>

    <!-- Section: Question type -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Question Type</span>
        </div>
        <div class="p-4 flex gap-3">
            <input type="radio" name="QuestionType" id="qt-mc" value="@((int)QuestionType.MultipleChoice)"
                   @(Model.QuestionType == QuestionType.MultipleChoice ? "checked" : "") class="hidden" />
            <input type="radio" name="QuestionType" id="qt-stmt" value="@((int)QuestionType.Statement)"
                   @(Model.QuestionType == QuestionType.Statement ? "checked" : "") class="hidden" />
            <input type="radio" name="QuestionType" id="qt-dnd" value="@((int)QuestionType.DragAndDrop)"
                   @(Model.QuestionType == QuestionType.DragAndDrop ? "checked" : "") class="hidden" />
            @{
                string mcCard = Model.QuestionType == QuestionType.MultipleChoice
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
                string stmtCard = Model.QuestionType == QuestionType.Statement
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
                string dndCard = Model.QuestionType == QuestionType.DragAndDrop
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
            }
            <button type="button" id="type-card-mc" onclick="selectTypeCard('@((int)QuestionType.MultipleChoice)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @mcCard">
                <div class="text-xl mb-1">☑</div>
                <div class="text-xs font-bold type-card-label">Multiple Choice</div>
                <div class="text-xs text-arcade-muted mt-0.5">Select one or more</div>
            </button>
            <button type="button" id="type-card-stmt" onclick="selectTypeCard('@((int)QuestionType.Statement)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @stmtCard">
                <div class="text-xl mb-1">⊙</div>
                <div class="text-xs font-bold type-card-label">Statement</div>
                <div class="text-xs text-arcade-muted mt-0.5">True / False</div>
            </button>
            <button type="button" id="type-card-dnd" onclick="selectTypeCard('@((int)QuestionType.DragAndDrop)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @dndCard">
                <div class="text-xl mb-1">⇄</div>
                <div class="text-xs font-bold type-card-label">Drag &amp; Drop</div>
                <div class="text-xs text-arcade-muted mt-0.5">Match pairs</div>
            </button>
        </div>
    </div>

    <!-- Section: Scoring (MC only) -->
    <div id="allow-multiple-section"
         class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden @(Model.QuestionType != QuestionType.MultipleChoice ? "hidden" : "")">
        <div class="px-4 py-2.5 border-b border-arcade-border flex items-center justify-between">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Scoring</span>
            <label class="flex items-center gap-2 cursor-pointer select-none">
                <input type="checkbox" asp-for="AllowMultipleAnswers" id="AllowMultipleAnswers"
                       class="accent-yellow-500 w-4 h-4" onchange="toggleScoringMode(this)" />
                <span class="text-sm text-arcade-muted">Allow multiple correct answers</span>
            </label>
        </div>
        <div id="scoring-mode-section" class="p-4 flex gap-3 @(Model.AllowMultipleAnswers ? "" : "hidden")">
            <input type="radio" name="ScoringMode" id="sm-aon" value="@((int)MultiAnswerScoringMode.AllOrNothing)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing ? "checked" : "") class="hidden" />
            <input type="radio" name="ScoringMode" id="sm-pc" value="@((int)MultiAnswerScoringMode.PartialCredit)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.PartialCredit ? "checked" : "") class="hidden" />
            @{
                string aonCard = Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing
                    ? "border-green-600 bg-green-950" : "border-arcade-border bg-arcade-dark hover:border-arcade-muted";
                string aonLabel = Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing
                    ? "text-green-400" : "text-arcade-muted";
                string pcCard = Model.ScoringMode == MultiAnswerScoringMode.PartialCredit
                    ? "border-green-600 bg-green-950" : "border-arcade-border bg-arcade-dark hover:border-arcade-muted";
                string pcLabel = Model.ScoringMode == MultiAnswerScoringMode.PartialCredit
                    ? "text-green-400" : "text-arcade-muted";
            }
            <button type="button" id="scoring-card-aon" onclick="selectScoringCard('@((int)MultiAnswerScoringMode.AllOrNothing)')"
                    class="scoring-card flex-1 rounded-lg border-2 px-4 py-3 text-left transition @aonCard">
                <div class="text-xs font-bold scoring-card-label mb-1 @aonLabel">All or Nothing</div>
                <div class="text-xs text-arcade-muted">Full points only if every correct answer is selected</div>
            </button>
            <button type="button" id="scoring-card-pc" onclick="selectScoringCard('@((int)MultiAnswerScoringMode.PartialCredit)')"
                    class="scoring-card flex-1 rounded-lg border-2 px-4 py-3 text-left transition @pcCard">
                <div class="text-xs font-bold scoring-card-label mb-1 @pcLabel">Partial Credit</div>
                <div class="text-xs text-arcade-muted">Points per correct pick, no wrong-answer penalty</div>
            </button>
        </div>
    </div>

    <!-- Section: Answer options / pairs -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border flex items-center justify-between">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase" id="options-label">
                @(Model.QuestionType == QuestionType.Statement ? "Statements"
                  : Model.QuestionType == QuestionType.DragAndDrop ? "Pairs"
                  : "Answer Options")
            </span>
            <span id="mc-hint" class="text-xs text-arcade-muted @(Model.QuestionType != QuestionType.MultipleChoice ? "hidden" : "")">Check = correct</span>
        </div>
        <div class="p-4">
            <div id="dnd-headers" class="grid grid-cols-[1fr_1fr_24px] gap-3 mb-2 px-0.5 @(Model.QuestionType != QuestionType.DragAndDrop ? "hidden" : "")">
                <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Label</span>
                <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Sentence</span>
                <span></span>
            </div>
            <div id="options-container" class="space-y-2">
                @for (int i = 0; i < Model.OptionTexts.Count; i++)
                {
                    bool isCorrect = Model.CorrectOptionIndices.Contains(i);
                    bool isDnD = Model.QuestionType == QuestionType.DragAndDrop;
                    bool isStmt = Model.QuestionType == QuestionType.Statement;
                    string rowBase = isDnD
                        ? "option-row grid grid-cols-[1fr_1fr_24px] gap-3 items-center"
                        : "option-row flex items-center gap-3 rounded-lg px-3 py-2 border bg-arcade-dark border-arcade-border";
                    <div class="@rowBase">
                        <input type="checkbox" name="CorrectOptionIndices" value="@i"
                               @(isCorrect ? "checked" : "") class="correct-input hidden" />
                        <button type="button" onclick="toggleCorrect(this)"
                                class="correct-toggle mc-only @(isStmt || isDnD ? "hidden" : "") w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 transition @(isCorrect ? "bg-green-600 border-green-600 text-white" : "border-slate-600 bg-transparent")">
                            <span class="correct-check text-xs font-bold @(isCorrect ? "" : "hidden")">✓</span>
                        </button>
                        <input type="text" name="MatchTexts[@i]"
                               value="@(Model.MatchTexts.Count > i ? Model.MatchTexts[i] : "")"
                               placeholder="Label @(i + 1)"
                               class="match-text dnd-only @(isDnD ? "" : "hidden") bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
                        <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
                               placeholder="@(isStmt ? $"Statement {i + 1}" : isDnD ? $"Sentence {i + 1}" : $"Option {i + 1}")"
                               class="option-text @(isDnD ? "bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 focus:border-fire" : "flex-1 bg-transparent border-none") text-arcade-text placeholder-arcade-muted focus:outline-none text-sm" />
                        <div class="stmt-only @(isStmt ? "" : "hidden") flex gap-1 flex-shrink-0">
                            <button type="button" onclick="setStmtYes(this)"
                                    class="stmt-yes px-3 py-1 text-sm rounded-lg border transition @(isCorrect ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">Yes</button>
                            <button type="button" onclick="setStmtNo(this)"
                                    class="stmt-no px-3 py-1 text-sm rounded-lg border transition @(!isCorrect ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">No</button>
                        </div>
                        <button type="button" onclick="removeOption(this)"
                                class="text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1 @(isDnD ? "" : "flex-shrink-0")">×</button>
                    </div>
                }
            </div>
            <button id="add-option-btn" type="button" onclick="addOption()"
                    class="mt-3 w-full px-4 py-2 text-sm border border-dashed border-arcade-border rounded-lg text-arcade-muted hover:border-fire hover:text-fire transition">
                @(Model.QuestionType == QuestionType.Statement ? "+ Add statement"
                  : Model.QuestionType == QuestionType.DragAndDrop ? "+ Add pair"
                  : "+ Add option")
            </button>
        </div>
    </div>

    <div asp-validation-summary="All" class="text-red-400 text-sm"></div>

    <button type="submit"
            class="w-full py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition text-base">
        Save Question
    </button>
    <a href="/manage/exams/@Model.ExamId/import"
       class="block text-center py-2.5 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition text-sm">
        Cancel
    </a>
</form>

@section Scripts {
<script>
function toggleScoringMode(checkbox) {
    document.getElementById('scoring-mode-section').classList.toggle('hidden', !checkbox.checked);
}

function switchQuestionType(value) {
    const isMC = value === '@((int)QuestionType.MultipleChoice)';
    const isDnD = value === '@((int)QuestionType.DragAndDrop)';
    const isStatement = value === '@((int)QuestionType.Statement)';
    document.getElementById('allow-multiple-section').classList.toggle('hidden', !isMC);
    if (isMC) {
        document.getElementById('scoring-mode-section').classList.toggle('hidden', !document.getElementById('AllowMultipleAnswers').checked);
    }
    document.querySelectorAll('.mc-only').forEach(el => el.classList.toggle('hidden', !isMC));
    document.querySelectorAll('.stmt-only').forEach(el => el.classList.toggle('hidden', !isStatement));
    document.querySelectorAll('.dnd-only').forEach(el => el.classList.toggle('hidden', !isDnD));
    document.getElementById('dnd-headers').classList.toggle('hidden', !isDnD);
    document.getElementById('mc-hint').classList.toggle('hidden', !isMC);
    document.getElementById('options-label').textContent =
        isStatement ? 'Statements' : isDnD ? 'Pairs' : 'Answer Options';
    document.getElementById('add-option-btn').textContent =
        isStatement ? '+ Add statement' : isDnD ? '+ Add pair' : '+ Add option';
    document.querySelectorAll('.option-text').forEach((el, i) => {
        el.placeholder = isStatement ? 'Statement ' + (i + 1) : isDnD ? 'Sentence ' + (i + 1) : 'Option ' + (i + 1);
    });
    document.querySelectorAll('.match-text').forEach((el, i) => { el.placeholder = 'Label ' + (i + 1); });
}

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
        if (match) { match.name = `MatchTexts[${i}]`; match.placeholder = 'Label ' + (i + 1); }
    });
}

function addOption() {
    const container = document.getElementById('options-container');
    const i = container.querySelectorAll('.option-row').length;
    const typeVal = document.querySelector('input[name="QuestionType"]:checked')?.value;
    const isStatement = typeVal === '@((int)QuestionType.Statement)';
    const isDnD = typeVal === '@((int)QuestionType.DragAndDrop)';
    const div = document.createElement('div');
    if (isDnD) {
        div.className = 'option-row grid grid-cols-[1fr_1fr_24px] gap-3 items-center';
        div.innerHTML = `
            <input type="checkbox" name="CorrectOptionIndices" value="${i}" class="correct-input hidden" />
            <button type="button" onclick="toggleCorrect(this)" class="correct-toggle mc-only hidden w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 border-slate-600 bg-transparent transition"><span class="correct-check text-xs font-bold hidden">✓</span></button>
            <input type="text" name="MatchTexts[${i}]" placeholder="Label ${i + 1}" class="match-text dnd-only bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <input type="text" name="OptionTexts[${i}]" placeholder="Sentence ${i + 1}" class="option-text bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <div class="stmt-only hidden flex gap-1 flex-shrink-0"><button type="button" onclick="setStmtYes(this)" class="stmt-yes px-3 py-1 text-sm rounded-lg border border-arcade-border text-arcade-muted transition">Yes</button><button type="button" onclick="setStmtNo(this)" class="stmt-no px-3 py-1 text-sm rounded-lg border border-fire text-fire transition">No</button></div>
            <button type="button" onclick="removeOption(this)" class="text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1">×</button>`;
    } else {
        div.className = 'option-row flex items-center gap-3 rounded-lg px-3 py-2 border bg-arcade-dark border-arcade-border';
        div.innerHTML = `
            <input type="checkbox" name="CorrectOptionIndices" value="${i}" class="correct-input hidden" />
            <button type="button" onclick="toggleCorrect(this)" class="correct-toggle mc-only${isStatement ? ' hidden' : ''} w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 border-slate-600 bg-transparent transition"><span class="correct-check text-xs font-bold hidden">✓</span></button>
            <input type="text" name="MatchTexts[${i}]" placeholder="Label ${i + 1}" class="match-text dnd-only hidden bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <input type="text" name="OptionTexts[${i}]" placeholder="${isStatement ? 'Statement' : 'Option'} ${i + 1}" class="option-text flex-1 bg-transparent border-none text-arcade-text placeholder-arcade-muted focus:outline-none text-sm" />
            <div class="stmt-only${isStatement ? '' : ' hidden'} flex gap-1 flex-shrink-0"><button type="button" onclick="setStmtYes(this)" class="stmt-yes px-3 py-1 text-sm rounded-lg border border-arcade-border text-arcade-muted transition">Yes</button><button type="button" onclick="setStmtNo(this)" class="stmt-no px-3 py-1 text-sm rounded-lg border border-fire text-fire transition">No</button></div>
            <button type="button" onclick="removeOption(this)" class="flex-shrink-0 text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1">×</button>`;
    }
    container.appendChild(div);
}

function removeOption(btn) { btn.closest('.option-row').remove(); reindexOptions(); }

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

function selectTypeCard(value) {
    const mcVal = '@((int)QuestionType.MultipleChoice)';
    const stmtVal = '@((int)QuestionType.Statement)';
    const dndVal = '@((int)QuestionType.DragAndDrop)';
    document.querySelectorAll('input[name="QuestionType"]').forEach(r => { r.checked = (r.value === String(value)); });
    [{ id: 'type-card-mc', val: mcVal }, { id: 'type-card-stmt', val: stmtVal }, { id: 'type-card-dnd', val: dndVal }]
        .forEach(({ id, val }) => {
            const card = document.getElementById(id);
            const active = val === String(value);
            card.classList.toggle('border-fire', active);
            card.classList.toggle('text-fire', active);
            card.classList.toggle('border-arcade-border', !active);
            card.classList.toggle('text-arcade-muted', !active);
        });
    switchQuestionType(String(value));
}

function selectScoringCard(value) {
    document.querySelectorAll('input[name="ScoringMode"]').forEach(r => { r.checked = (r.value === String(value)); });
    const aonActive = value === '@((int)MultiAnswerScoringMode.AllOrNothing)';
    [{ id: 'scoring-card-aon', active: aonActive }, { id: 'scoring-card-pc', active: !aonActive }]
        .forEach(({ id, active }) => {
            const card = document.getElementById(id);
            card.classList.toggle('border-green-600', active);
            card.classList.toggle('bg-green-950', active);
            card.classList.toggle('border-arcade-border', !active);
            card.classList.toggle('bg-arcade-dark', !active);
            const lbl = card.querySelector('.scoring-card-label');
            lbl.classList.toggle('text-green-400', active);
            lbl.classList.toggle('text-arcade-muted', !active);
        });
}

function toggleCorrect(btn) {
    const row = btn.closest('.option-row');
    const cb = row.querySelector('.correct-input');
    const isNowCorrect = !cb.checked;
    const allowMultiple = document.getElementById('AllowMultipleAnswers')?.checked ?? false;
    if (isNowCorrect && !allowMultiple) {
        document.querySelectorAll('#options-container .option-row').forEach(otherRow => {
            if (otherRow === row) return;
            const otherCb = otherRow.querySelector('.correct-input');
            if (!otherCb || !otherCb.checked) return;
            otherCb.checked = false;
            const otherBtn = otherRow.querySelector('.correct-toggle');
            if (otherBtn) {
                otherBtn.classList.remove('bg-green-600', 'border-green-600', 'text-white');
                otherBtn.classList.add('border-slate-600', 'bg-transparent');
                const otherCheck = otherBtn.querySelector('.correct-check');
                if (otherCheck) otherCheck.classList.add('hidden');
            }
        });
    }
    cb.checked = isNowCorrect;
    btn.classList.toggle('bg-green-600', isNowCorrect);
    btn.classList.toggle('border-green-600', isNowCorrect);
    btn.classList.toggle('text-white', isNowCorrect);
    btn.classList.toggle('border-slate-600', !isNowCorrect);
    btn.classList.toggle('bg-transparent', !isNowCorrect);
    const check = btn.querySelector('.correct-check');
    if (check) check.classList.toggle('hidden', !isNowCorrect);
}
</script>
}
```

- [ ] **Step 3: Build the full solution**

```
dotnet build src/Examageddon.slnx
```

Expected: 0 errors.

- [ ] **Step 4: Run all tests**

```
dotnet test src/Examageddon.Tests/
```

Expected: all tests pass.

---

## Manual Smoke Test Checklist

After all tasks pass:

1. Start the app: `dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj`
2. Navigate to `/manage`, open an exam's Questions page
3. Click **↓ Download Template** — browser should download `question-template.json` with 4 example questions
4. Upload the downloaded template — should redirect to `/manage/exams/{id}/import` showing 4 staged questions
5. Click **Edit** on a question — verify the form opens pre-populated, changes save back to preview
6. Click **Remove** on a question — verify it's removed from the list
7. Click **Import 3 Questions** (or however many remain) — verify questions appear on the Questions page
8. Upload a file with invalid JSON (e.g., `{"type":"bad"}`) — verify error list appears on the Questions page, nothing stored
9. Upload a JSON array with validation errors (e.g., MC question with no correct answer) — verify all errors listed
