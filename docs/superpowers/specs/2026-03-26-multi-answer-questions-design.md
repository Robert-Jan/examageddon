# Multi-Answer Questions Design

**Date:** 2026-03-26
**Status:** Approved

## Overview

Add support for questions that accept multiple correct answers. Questions can optionally require students to select all correct options, with either all-or-nothing or partial credit scoring. The manager UI gains controls for configuring this per-question; the session UI switches from immediate radio-style buttons to checkboxes with an explicit Submit button.

---

## 1. Data Model

### New enum — `MultiAnswerScoringMode` (`Examageddon.Data/Enums/`)

```csharp
public enum MultiAnswerScoringMode
{
    AllOrNothing = 0,  // all correct options selected, no incorrect ones
    PartialCredit = 1, // (correct_selected - wrong_selected) / total_correct, clamped to [0,1]
}
```

### `Question` — two new properties

| Property | Type | Default | Notes |
|---|---|---|---|
| `AllowMultipleAnswers` | `bool` | `false` | Enables multi-select mode |
| `ScoringMode` | `MultiAnswerScoringMode` | `AllOrNothing` | Only meaningful when `AllowMultipleAnswers = true` |

`AnswerOption.IsCorrect` is already a bool per option — no changes needed there; it naturally supports marking multiple options correct.

### New entity — `SessionAnswerSelection`

```csharp
public class SessionAnswerSelection
{
    public int Id { get; set; }
    public int SessionAnswerId { get; set; }
    public SessionAnswer SessionAnswer { get; set; } = null!;
    public int AnswerOptionId { get; set; }
    public AnswerOption AnswerOption { get; set; } = null!;
}
```

### `SessionAnswer` — changes

- **Remove** `SelectedAnswerOptionId` and `SelectedAnswerOption`
- **Add** `ICollection<SessionAnswerSelection> Selections`
- **Add** `double Score` (0.0–1.0; replaces direct use of `IsCorrect` for scoring arithmetic)
- **Keep** `bool IsCorrect` (true if `Score > 0` for partial credit, or exact match for all-or-nothing)

### `ExamSession` — change

- `CorrectAnswers`: `int` → `double` to accumulate partial scores (e.g. 7.5 / 10)

### Schema migration

The app uses `EnsureCreated()` with no migration files. Delete `database/examageddon.db` on next run to apply the new schema.

---

## 2. Service & Repository Layer

### `IExamSessionRepository` changes

- `GetAnswerAsync(sessionId, questionId)` — must `.Include(sa => sa.Selections)` in the query
- Replace `GetAnswerOptionAsync(int id)` with `GetAnswerOptionsAsync(IEnumerable<int> ids)` — returns all matching options in one query

### `IExamRepository` changes

- Add `GetQuestionWithOptionsAsync(int questionId)` — returns the question with all `AnswerOptions` eagerly loaded (needed by `ExamSessionService` for scoring)

### `ExamSessionService.SubmitAnswerAsync`

New signature:
```csharp
Task SubmitAnswerAsync(int sessionId, int questionId, IReadOnlyList<int> answerOptionIds)
```

Logic:
1. Load question via `IExamRepository.GetQuestionWithOptionsAsync` to get `AllowMultipleAnswers`, `ScoringMode`, and which options are correct.
2. Compute `Score` and `IsCorrect`:
   - **AllOrNothing** (or single-answer): `isCorrect = selectedIds.SetEquals(correctIds)`, `score = isCorrect ? 1.0 : 0.0`
   - **PartialCredit**: `score = Max(0, (correctSelected - wrongSelected) / totalCorrect)`, `isCorrect = score > 0`
3. Load existing `SessionAnswer` (with Selections). If found, replace `Selections` and update `Score`/`IsCorrect`/`AnsweredAt`. Otherwise create new `SessionAnswer` with Selections.

### `CompleteSessionAsync`

```csharp
session.CorrectAnswers = session.SessionAnswers.Sum(a => a.Score);
```

### Model changes

**`SessionQuestionModel`:**
- `int? ExistingAnswerOptionId` → `IReadOnlySet<int> SelectedAnswerOptionIds` (empty set when not answered)
- `bool IsAnswered` → `SelectedAnswerOptionIds.Count > 0`
- Add `bool AllowMultipleAnswers` (forwarded from `Question`)

**`QuestionResultItem`:**
- `AnswerOption? SelectedOption` → `IReadOnlyList<AnswerOption> SelectedOptions`

---

## 3. Manager UI (QuestionForm)

### `QuestionForm.cshtml` changes

- Add `AllowMultipleAnswers` checkbox
- Add `ScoringMode` radio group (`All or nothing` / `Partial credit`) — hidden by default, revealed via JS when the checkbox is ticked
- Change answer option correct indicator: `<input type="radio" name="CorrectOptionIndex">` → `<input type="checkbox" name="CorrectOptionIndices">`
- Update label copy: "mark the correct one" → "mark the correct one(s)"

### `QuestionForm.cshtml.cs` changes

- Add `[BindProperty] bool AllowMultipleAnswers`
- Add `[BindProperty] MultiAnswerScoringMode ScoringMode`
- Replace `[BindProperty] int CorrectOptionIndex` → `[BindProperty] List<int> CorrectOptionIndices`
- Option building: `IsCorrect = CorrectOptionIndices.Contains(idx)`
- On GET (edit): populate `CorrectOptionIndices` from all options where `IsCorrect == true`; populate `AllowMultipleAnswers` and `ScoringMode` from `ExistingQuestion`
- On POST: set `question.AllowMultipleAnswers` and `question.ScoringMode` before save

---

## 4. Session UI

### `_AnswerFeedback.cshtml`

Branches on `Model.AllowMultipleAnswers`:

**Single-answer (existing behaviour, minor tweak):**
- HTMX button click posts `answerIds=@option.Id` (was `answerId`)

**Multi-answer, not yet answered:**
- Checkboxes inside `<form id="multi-answer-form">`
- "Submit Answer" button uses `hx-include="#multi-answer-form"` to post all checked `answerIds` values
- `hx-target="#answer-container"`, `hx-swap="innerHTML"`

**Multi-answer, answered:**
- Non-interactive locked display; checkboxes shown checked/unchecked per `SelectedAnswerOptionIds`
- Green highlight for correct options, red for selected-but-wrong, muted for unselected-wrong (same colour logic as single-answer)

### `Question.cshtml.cs` changes

- `OnPostAnswerAsync`: replace `int answerId` parameter with `List<int> answerIds`; pass list to `SubmitAnswerAsync`

### Result & Review pages

- Anywhere `SelectedOption` is rendered, iterate `SelectedOptions` instead
- Score display handles `double CorrectAnswers` (renders e.g. `7.5 / 10` for partial credit)

---

## Out of Scope

- Minimum number of selections enforcement in the UI (e.g. "select at least 2")
- Per-option partial credit weights
- Backwards compatibility shims for existing session data (dev DB is recreated from scratch)
