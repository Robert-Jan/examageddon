# Azure Certification Scoring Preset — Design Spec

**Date:** 2026-03-27
**Status:** Approved

## Overview

Add an `AzureCertification` scoring preset to Examageddon that faithfully reproduces how Microsoft Azure certification exams score candidates: 1–1000 scaled score, 700 pass threshold, partial credit without wrong-answer deductions, and sensible session defaults pre-filled on the setup page.

---

## Background: How Azure Scoring Works

Key facts from official Microsoft Learn documentation:

- **Score scale:** 1–1000 (scaled, not raw percentage)
- **Pass threshold:** 700 for all role-based, fundamentals, and specialty exams (universal)
- **Scaled ≠ 70% raw:** A scaled score of 700 does not mean 70% of questions answered correctly. Harder question sets require fewer correct answers to reach 700; easier sets require more. (Our implementation approximates this with a linear mapping.)
- **Question weighting:** Almost always 1 point per question; multi-component questions earn 1 point per correct component
- **Partial credit:** Yes — each correct component in multi-select and drag-and-drop questions earns its own point
- **No wrong-answer deduction:** Selecting an incorrect option in a multi-select question does not remove points; it simply fails to add them
- **No guessing penalty:** Microsoft explicitly confirms no points are deducted for incorrect answers
- **Feedback:** No per-question feedback during the exam; results are shown at the end
- **Question draw:** Limited count, randomized order

---

## Data Model Changes

### New enum: `ScoringPreset`

```csharp
// Examageddon.Data/Enums/ScoringPreset.cs
public enum ScoringPreset
{
    None = 0,
    AzureCertification = 1,
}
```

### `Exam` entity

- `PassingScorePercent` → renamed to `PassingThreshold` (same `int` type)
  - For `None`: value is 0–100 (percentage)
  - For `AzureCertification`: value is 0–1000 (scaled score, default 700)
- New property: `ScoringPreset ScoringPreset { get; set; }`

### `ExamSession` entity

- New property: `ScoringPreset ScoringPreset { get; set; }`
- Snapshotted from `exam.ScoringPreset` at session creation time
- Ensures scoring logic and result display are always consistent with the rules active when the session started, even if the exam is later edited

### Database

Dev database (`database/examageddon.db`) must be deleted and recreated via `EnsureCreated()` after this schema change.

---

## Scoring Logic Changes

All changes are in `ExamSessionService`.

### `CreateSessionAsync`

Copy `exam.ScoringPreset` onto the new `ExamSession.ScoringPreset` field.

### `SubmitAnswerAsync` — multi-answer partial credit

When `session.ScoringPreset == AzureCertification` and the question uses `PartialCredit` scoring:

| Formula | Current (`None`) | Azure |
|---|---|---|
| Score | `(correctSelected - wrongSelected) / correctIds.Count` | `correctSelected / (double)correctIds.Count` |
| Wrong picks | Deducted | Ignored (no penalty) |

Single-answer questions and `AllOrNothing` mode are unaffected by the preset.

To access the preset inside `SubmitAnswerAsync`, load the session via the existing `sessionRepo.GetByIdAsync(sessionId)`.

### `SubmitDragAnswerAsync` — drag-and-drop partial credit

The current drag partial credit formula (`correctCount / totalPairs`) is already deduction-free, so no code change is needed here. The preset does not affect drag-and-drop scoring.

### `CompleteSessionAsync` — pass/fail calculation

For `AzureCertification`:
- Compute: `scaledScore = (int)Math.Round(rawScore / totalQuestions * 1000)`
- Pass if `scaledScore >= session.Exam.PassingThreshold` (e.g., 700)
- Store `session.CorrectAnswers = rawScore` (the raw point total, same as `None` — scaling is always computed on the fly in `SessionResultModel.ScaledScore`)

For `None`: existing percentage logic is unchanged.

---

## `SessionResultModel` Changes

| Before | After |
|---|---|
| `int PassingScorePercent` | `int PassingThreshold` |
| — | `ScoringPreset ScoringPreset` |
| — | `int ScaledScore => (int)Math.Round(CorrectAnswers / TotalQuestions * 1000)` |

`ScorePercent` is retained for `None` preset display.

---

## UI Changes

### Exam creation form (`ExamForm.cshtml` + `ExamFormModel`)

- Rename `PassingScorePercent` label and input binding to `PassingThreshold`
- Add hidden input bound to `Exam.ScoringPreset`
- Add "Apply Azure Preset" button (type="button", no form submit):
  - Sets `PassingThreshold = 700`
  - Sets `ExamQuestionCount = 40`
  - Sets hidden `ScoringPreset = 1` (AzureCertification)
  - Shows a visible badge/label confirming the active preset
- Validation: widen `PassingThreshold` range to 1–1000 (JS enforces the correct range per preset; server accepts 1–1000 generically)

### Session setup page (`Setup.cshtml`)

When `Model.Exam.ScoringPreset == AzureCertification`, pre-select:
- `QuestionMode = Limited`
- `OrderMode = Random`
- `FeedbackMode = AtEnd`

User can still change any option freely before starting.

### Result page (`Result.cshtml`)

| Preset | Score display | Passing display |
|---|---|---|
| `None` | `@r.ScorePercent%` | `@r.PassingThreshold%  To Pass` |
| `AzureCertification` | `@r.ScaledScore / 1000` | `700  To Pass (scaled)` |

Pass/fail heading, colours, and question breakdown are unchanged.

---

## Out of Scope

- Skill-area breakdown bar chart (as shown in real Azure score reports)
- IRT-based non-linear scaling (linear approximation is used instead)
- Beta/unscored question simulation
- Lab-style performance-based questions
- MOS exam support
