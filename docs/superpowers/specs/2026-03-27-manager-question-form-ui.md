# Manager Question Form UI Redesign — Design Spec

**Date:** 2026-03-27
**Status:** Approved

## Overview

Redesign `QuestionForm.cshtml` (create/edit question) from a flat, cramped single-page form into a clean card-sectioned layout. No workflow changes, no page model changes — pure visual/HTML/CSS/JS improvement.

---

## Problem

The current form has three issues:
- **Cluttered:** All sections run together visually with no separation between question text, type, scoring, and answer options.
- **Cramped:** DragAndDrop pair inputs use a tiny fixed-width label field next to a full-width sentence field — both undersized.
- **Rough:** The image upload is an unstyled browser `<input type="file">`. Answer options look like plain checkboxes in a list.

---

## Approach

**Section cards + component upgrades.** Keep the single-column layout and all existing Razor model bindings, HTMX behaviour, and JS logic. Replace the visual treatment:

- Each logical group gets its own card (rounded border, section label header).
- The question type selector becomes three icon cards.
- Scoring options become two descriptive cards.
- Answer option rows get a styled inline correct/wrong toggle.
- DragAndDrop pairs get a proper two-column grid.
- Image upload gets a drop zone.

---

## Card Structure

Every section follows the same card pattern:

```html
<div class="section-card">
  <div class="section-header">
    <span class="section-label">LABEL</span>
    <!-- optional right-side controls -->
  </div>
  <div class="section-body">
    <!-- content -->
  </div>
</div>
```

CSS for these classes is added inline in `QuestionForm.cshtml` (Tailwind classes — no new stylesheet needed).

---

## Section: Question

A single `<textarea>` inside a card. No functional change.

```
┌─ QUESTION ──────────────────────────────────────────────────┐
│  [ textarea: question text, min 3 rows, resizable vertical ] │
└──────────────────────────────────────────────────────────────┘
```

---

## Section: Question Type

Three equal-width icon cards in a row. Clicking one sets the hidden `QuestionType` input and triggers the existing `switchQuestionType()` JS. The selected card gets a blue border + blue tint background; unselected cards are dark/muted.

```
┌─ QUESTION TYPE ─────────────────────────────────────────────┐
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  ☑           │  │  ⊙           │  │  ⇄           │       │
│  │  Multiple    │  │  Statement   │  │  Drag & Drop │       │
│  │  Choice      │  │  True/False  │  │  Match pairs │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└──────────────────────────────────────────────────────────────┘
```

The existing `<input type="hidden" asp-for="QuestionType">` is kept. The card click handler sets its value and fires `switchQuestionType()`.

---

## Section: Scoring *(Multiple Choice only)*

Shown only when `QuestionType == MultipleChoice` (same condition as today). Two sub-sections:

**"Allow multiple correct answers" checkbox** sits in the section header (right side). Clicking it toggles the existing `AllowMultipleAnswers` hidden input and shows/hides the scoring mode cards below.

**Scoring mode** (visible only when multiple answers allowed): two descriptive cards side by side — "All or Nothing" and "Partial Credit". Selected card gets a green border + green tint. Clicking sets the existing `ScoringMode` hidden input.

```
┌─ SCORING ─────────────────────── ☐ Allow multiple answers ─┐
│  ┌─────────────────────┐  ┌─────────────────────┐           │
│  │  All or Nothing     │  │  ✓ Partial Credit   │           │
│  │  Full points only   │  │  Per correct pick,  │           │
│  │  if all correct     │  │  no wrong penalty   │           │
│  └─────────────────────┘  └─────────────────────┘           │
└──────────────────────────────────────────────────────────────┘
```

---

## Section: Answer Options *(Multiple Choice only)*

Each option row:
- **Correct toggle** — a 20×20 px square button. When marked correct: green filled background + ✓ icon; when not: dark background + grey border. Clicking toggles the corresponding `CorrectOptionIndices[]` checkbox (keep the hidden checkbox for model binding).
- **Text input** — `flex: 1`, transparent background, no border (the row itself is the visual container).
- **Remove button** — `×` icon, right side, calls existing `removeOption()`.
- **Row background** — correct rows get a subtle green tint; incorrect rows are dark.

"Add option" is a full-width dashed-border button at the bottom of the section, calls existing `addOption()`.

---

## Section: Pairs *(DragAndDrop only)*

A two-column grid with equal-width columns and column headers above the first row.

```
┌─ PAIRS ─────────────────────────────────────────────────────┐
│  LABEL (draggable)          DROP TARGET / SENTENCE           │
│  ┌────────────────────┐  ┌────────────────────┐  ×          │
│  │ Azure Blob Storage │  │ Stores unstructured│             │
│  └────────────────────┘  └────────────────────┘             │
│  ┌────────────────────┐  ┌────────────────────┐  ×          │
│  │ Azure Functions    │  │ Runs serverless... │             │
│  └────────────────────┘  └────────────────────┘             │
│  [ + Add pair ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ]            │
└──────────────────────────────────────────────────────────────┘
```

Grid: `grid-template-columns: 1fr 1fr 24px`. The existing `MatchTexts[]` bindings are kept; only the layout changes.

---

## Section: Statement *(Statement only)*

No change to the Yes/No toggle logic. It will be placed inside a card like all other sections.

```
┌─ ANSWER ────────────────────────────────────────────────────┐
│  [ Yes ]  [ No ]                                             │
└──────────────────────────────────────────────────────────────┘
```

---

## Section: Image *(always visible)*

A styled drop zone replaces the browser's raw `<input type="file">`. The hidden `<input type="file">` remains for model binding; the drop zone triggers it via JS click.

- Default state: dashed border, upload icon, "Drop image here or browse" text.
- With image loaded (existing or newly selected): show the image preview (the current `<img id="imagePreview">` approach is kept, just styled to appear inside the drop zone).
- Drop zone supports `dragover` / `drop` events to accept image files and pass them to the hidden input.

---

## What Is Not Changing

- Razor page model (`QuestionForm.cshtml.cs`) — no changes.
- All existing `asp-for` bindings and hidden inputs.
- All existing JS functions: `addOption()`, `removeOption()`, `reindexOptions()`, `switchQuestionType()`, `addPair()`, `removePair()`.
- POST/GET behavior, validation attributes, antiforgery.
- `Questions.cshtml` (question list) — out of scope.
- `Index.cshtml`, `ExamForm.cshtml` — out of scope.

---

## Out of Scope

- Drag-to-reorder answer options
- Inline validation styling beyond what Razor already provides
- Mobile/responsive layout (manager is desktop-only)
- Any changes to the question list page
