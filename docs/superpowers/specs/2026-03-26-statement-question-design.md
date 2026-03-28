# Statement Question Type Design

**Date:** 2026-03-26
**Status:** Approved

## Overview

Add a Statement question type where each question contains multiple statements and students answer Yes or No per statement. Scoring uses the same AllOrNothing / PartialCredit modes already present for multi-answer questions.

---

## 1. Data Model

### `QuestionType` enum — add `Statement = 1`

```csharp
public enum QuestionType
{
    MultipleChoice = 0,
    Statement = 1,
}
```

No DB schema changes required. SQLite stores enums as integers; the existing `QuestionType` column already holds the value.

### `AnswerOption` — no changes

`Text` holds the statement text. `IsCorrect = true` means the correct answer is **Yes**; `IsCorrect = false` means the correct answer is **No**.

### `Question` — no changes

Statement questions are always created with `AllowMultipleAnswers = true` (enforced on save in the manager, not a model constraint). `ScoringMode` remains configurable (AllOrNothing or PartialCredit).

### `SessionAnswerSelection` / scoring — no changes

Submitting a statement question sends the IDs of statements the student said "Yes" to. This is identical to multi-answer submission. The existing scoring logic in `ExamSessionService.SubmitAnswerAsync` handles it correctly without modification.

---

## 2. Service & Model Layer

### `SessionQuestionModel` — add one property

```csharp
public QuestionType QuestionType { get; set; }
```

### `ExamSessionService.GetSessionQuestionAsync` — populate the new property

```csharp
QuestionType = sq.Question.QuestionType,
```

No other service changes. `SubmitAnswerAsync` requires no changes.

---

## 3. Manager UI

### `QuestionForm.cshtml.cs` changes

- Add `[BindProperty] public QuestionType QuestionType { get; set; }`
- On POST: if `QuestionType == Statement`, force `AllowMultipleAnswers = true` before saving
- On POST: skip the "at least one correct option" validation for Statement questions — having zero Yes-correct statements is valid (all correct answers are No)
- On POST: for Statement questions, validate instead that at least one statement (option with non-blank text) exists
- On GET (edit): populate `QuestionType` from `ExistingQuestion.QuestionType`

### `QuestionForm.cshtml` changes

Add a **Question Type** radio selector at the top of the form (Multiple Choice / Statement).

JavaScript (`switchQuestionType(value)`) toggles visibility:

| Section | Multiple Choice | Statement |
|---|---|---|
| AllowMultipleAnswers checkbox | visible | hidden |
| ScoringMode section | shown when AllowMultipleAnswers checked | always visible |
| Option correct indicator | checkbox (`CorrectOptionIndices`) | Yes/No radio per row |
| Section label | "Answer Options" | "Statements" |
| Add button | "+ Add option" | "+ Add statement" |

For Statement type, each option row shows:
- Statement text input
- Yes radio (`name="CorrectOptionIndices" value="@i"`) — included = correct answer is Yes
- No radio (default selected) — not included = correct answer is No
- Remove button

New statement rows added via JS default the No radio to checked, so no statement can be left in an unselected state.

`CorrectOptionIndices` still drives `IsCorrect` on save — a statement with Yes selected has its index included, same as a multiple-choice option with its checkbox checked.

---

## 4. Session UI

### `_AnswerFeedback.cshtml` — new top-level branch

Branch on `Model.Question.QuestionType == QuestionType.Statement` before the existing `AllowMultipleAnswers` branch.

**Not yet answered:**

```
<form id="statement-form">
  [for each statement]
    statement text | [Yes] [No]   ← toggle buttons, one hidden checkbox name="answerIds" value="@option.Id"
</form>
<button disabled id="stmt-submit" hx-post="..." hx-include="#statement-form">Submit</button>
```

- Clicking Yes checks the hidden checkbox and highlights the Yes button
- Clicking No unchecks it and highlights the No button
- JS enables the submit button once every statement has a selection (`answeredCount == totalCount`)

**Already answered:**

Locked rows per statement showing which answer was selected. In Direct feedback mode: correct = green, wrong = red. In AtEnd mode: selected answer highlighted, no colours until result page.

### Result & Review pages — no changes

Both already iterate `SelectedOptions` and display correct answers via `AnswerOptions.Where(a => a.IsCorrect)`. This works identically for Statement questions.

---

## Out of Scope

- Mixing Yes/No and free-text in the same question
- Requiring a minimum number of statements
- Per-statement scoring weights
