# Drag & Drop Question Type — Design Spec

Date: 2026-03-27

## Overview

Add a `DragAndDrop` question type to Examageddon. The student sees a pool of shuffled draggable labels and a list of sentences, each with a drop zone on the left. They drag each label onto the correct sentence. It is a strict 1-to-1 matching with no distractors. Scoring mode (all-or-nothing or partial credit) is configurable per question, matching the existing `ScoringMode` pattern.

---

## Data Model

### `QuestionType` enum
Add `DragAndDrop = 2` to `Examageddon.Data/Enums/QuestionType.cs`.

### `AnswerOption` entity
Add one nullable column:

```csharp
public string? MatchText { get; set; }
```

- `Text` = the sentence shown on the right (e.g. "Capital of France")
- `MatchText` = the draggable label shown in the pool (e.g. "Paris")
- `IsCorrect` = always `true` for DragAndDrop (no distractors; set automatically on save)
- `MatchText` is `null` for all other question types

### `SessionAnswerSelection` entity
Add one nullable column:

```csharp
public int? PlacedOptionId { get; set; }
```

- For DragAndDrop: `AnswerOptionId` = the sentence slot's option, `PlacedOptionId` = what the student dragged into that slot
- Correct when `PlacedOptionId == AnswerOptionId`
- `null` for all other question types

Both columns are nullable, so `EnsureCreated()` handles them on first run. Existing dev databases need recreation.

---

## Service Layer

### `SessionQuestionModel` — new property
Add to `Examageddon.Services/Models/SessionQuestionModel.cs`:

```csharp
public IReadOnlyDictionary<int, int> DragPlacements { get; set; } = new Dictionary<int, int>();
```

Maps `slotOptionId → placedOptionId` for DragAndDrop questions. Empty for all other types. Used by the session UI to pre-fill slots when revisiting an already-answered question (AtEnd mode).

### `ExamSessionService.GetSessionQuestionAsync`
Populate `DragPlacements` from the stored selections when `QuestionType == DragAndDrop`:

```csharp
DragPlacements = sq.Question.QuestionType == QuestionType.DragAndDrop && answer is not null
    ? answer.Selections
        .Where(s => s.PlacedOptionId.HasValue)
        .ToDictionary(s => s.AnswerOptionId, s => s.PlacedOptionId!.Value)
    : new Dictionary<int, int>(),
```

Also add shuffling of `AnswerOptions` for `DragAndDrop` questions:

```csharp
AnswerOptions = sq.Question.QuestionType == QuestionType.DragAndDrop
    ? [.. sq.Question.AnswerOptions.OrderBy(_ => Guid.NewGuid())]
    : [.. sq.Question.AnswerOptions.OrderBy(a => a.OrderIndex)],
```

This shuffles the pool on every page load. Order is not persisted.

### `ExamSessionService.SubmitDragAnswerAsync`
New method:

```csharp
public async Task SubmitDragAnswerAsync(
    int sessionId,
    int questionId,
    IReadOnlyList<int> slotIds,
    IReadOnlyList<int> placedIds)
```

- `slotIds[i]` = the `AnswerOptionId` of sentence slot i
- `placedIds[i]` = the `AnswerOptionId` of the label dropped into slot i
- Correct per pair: `placedIds[i] == slotIds[i]`
- Scoring follows `question.ScoringMode`:
  - `AllOrNothing`: `score = allCorrect ? 1.0 : 0.0`
  - `PartialCredit`: `score = correctCount / totalCount`
- `isCorrect = score > 0`
- Stores one `SessionAnswerSelection` per slot: `AnswerOptionId = slotId`, `PlacedOptionId = placedId`
- Upserts like `SubmitAnswerAsync` (clears existing selections if re-answering)

---

## Web Layer

### `QuestionModel` — new handler
```csharp
public async Task<IActionResult> OnPostDragAnswerAsync(
    int sessionId, int n, int questionId,
    List<int> slotIds, List<int> placedIds)
```

Calls `SubmitDragAnswerAsync`, then reloads `SessionQuestion` and returns `Partial("_AnswerFeedback", ...)`. Same pattern as `OnPostAnswerAsync`.

### `QuestionFormModel` — new binding property
```csharp
[BindProperty]
public List<string> MatchTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];
```

Parallel to `OptionTexts[]`. On POST for `DragAndDrop`:
- Auto-set all `IsCorrect = true`
- Auto-set `AllowMultipleAnswers = true`
- Set `ScoringMode` from the form binding (already exists)
- Validate: at least one pair with both fields non-empty

---

## Session UI (`_AnswerFeedback.cshtml`)

### Unanswered state
```
[Pool]  shuffled draggable chips (orange outlined, draggable/tappable)

[Slot]  ○ drop zone   Sentence text
[Slot]  ■ Paris ×     Sentence text       ← placed chip, click × or drag back to unplace
[Slot]  ○ drop here   Sentence text       ← highlighted when a chip is selected (touch)
[Slot]  ○ drop zone   Sentence text

[Submit Answer]  disabled until all slots filled
```

Form posts to `?handler=DragAnswer` with parallel `slotIds[]` and `placedIds[]` hidden inputs built by JS before submit.

### Answered state — Direct feedback
Each row shows ✓/✗, the placed chip, and (if wrong) the correct chip below it. Pool is gone. Next/Finish button rendered as usual.

### Answered state — AtEnd mode
Each row shows the placed chip without color feedback. No Next button (existing Back/Next navigation handles it).

### Drag + touch interaction (vanilla JS, no library)
**Drag API (desktop):**
- `dragstart` on pool chips sets `dataTransfer` with the option ID
- `dragover`/`drop` on slots accepts the chip, moves it from pool DOM to slot
- Dropping a chip onto an occupied slot swaps: the displaced chip returns to pool

**Touch fallback (mobile):**
- `touchstart` on a pool chip: marks it as selected (glows orange border)
- `touchstart` on an empty slot: places the selected chip there
- `touchstart` on a placed chip: returns it to the pool (deselects)
- `touchstart` on another pool chip while one is selected: switches selection
- No `touchmove` tracking needed — tap-to-select, tap-to-place

Both paths share the same DOM mutation logic (move chip node, update hidden inputs). Submit button enabled/disabled by checking slot fill count after each interaction.

---

## Manager Form (`QuestionForm.cshtml` / `.cshtml.cs`)

### New radio button
```html
<input type="radio" name="QuestionType" value="2" onchange="switchQuestionType(this.value)" />
Drag & Drop
```

### When DragAndDrop is selected
- Hide "Allow multiple answers" checkbox (same as Statement)
- Show Scoring Mode section (same as Statement)
- Replace options list with two-column pairs:
  - Column 1 (narrow): `MatchTexts[i]` — "Label" placeholder
  - Column 2 (wide): `OptionTexts[i]` — "Sentence" placeholder
  - `CorrectOptionIndices` checkboxes hidden (auto-set server-side)
- "+ Add pair" button adds a row to both arrays
- Label changed to "Pairs (label → sentence)"

### Validation
- At least one pair with both label and sentence non-empty
- `CorrectOptionIndices` auto-filled to all indices before save (server-side)

---

## Result Page (`Result.cshtml`)

DragAndDrop questions display per-slot in the question result item:

```
✅ Paris        → Capital of France
❌ Rome  (✗)   → Capital of Germany   [correct: Berlin]
✅ London       → Capital of England
❌ Berlin (✗)  → Capital of Italy     [correct: Rome]
```

### `QuestionResultItem` — new property
Add to `Examageddon.Services/Models/QuestionResultItem.cs`:

```csharp
public IReadOnlyDictionary<int, AnswerOption> DragPlacements { get; set; } = new Dictionary<int, AnswerOption>();
```

Maps `slotOptionId → the AnswerOption that was placed there`. Populated in `GetResultAsync` for DragAndDrop questions by looking up each selection's `PlacedOptionId` against the question's `AnswerOptions` collection.

The result page renders a DragAndDrop-specific branch: for each slot option (the sentence), look up `item.DragPlacements[slot.Id]` to get the placed label, compare `placed.Id == slot.Id` to determine correct/wrong, and show `slot.MatchText` as the correct label when wrong.

---

## Review Page (`Review.cshtml`)

The summary line for a DragAndDrop question shows the user's assignments:
```
Paris → Capital of France, Rome → Capital of Germany…
```
Built from `item.DragPlacements`: for each entry, render `placedOption.MatchText + " → " + slot.Text` (slot looked up via `item.Question.AnswerOptions`). Clicking navigates back to the DragAndDrop UI with slots pre-filled.

Pre-fill on page load: `GetSessionQuestionAsync` populates `DragPlacements` (slotId → placedId). The UI renders each slot's chip from `Model.DragPlacements[slotOptionId]` if an entry exists, and returns the remaining unplaced options to the pool.

---

## Testing

Add to `ExamSessionServiceTests`:

- `SubmitDragAnswer_AllCorrect_ScoresOne` — all pairs placed correctly, partial mode
- `SubmitDragAnswer_PartialCorrect_ScoresPartial` — some wrong, partial credit
- `SubmitDragAnswer_AllWrong_AllOrNothing_ScoresZero` — all wrong, all-or-nothing
- `SubmitDragAnswer_PartialCorrect_AllOrNothing_ScoresZero` — some wrong, all-or-nothing

---

## Out of scope

- Distractor items (no-match labels)
- Image-based draggable chips
- Reordering (sort) question type (different interaction, different spec)
