# Question JSON Import — Design Spec

**Date:** 2026-03-27
**Status:** Approved

## Overview

Add a JSON import feature to the Manage area that lets a user upload a JSON file of questions into an existing exam. Questions are validated up-front, staged in session memory for review and editing, and only written to the database when the user confirms the import.

---

## User Flow

```
Questions page (/manage/exams/{id}/questions)
  ├─ Download Template  →  GET returns question-template.json (file attachment)
  └─ Import JSON        →  inline upload form on the Questions page
        ↓ POST /manage/exams/{id}/import/upload
        ├─ Validation errors  →  re-render Questions page with error list (nothing stored)
        └─ Valid              →  store in session → redirect to
              /manage/exams/{id}/import          (preview list)
                ├─ Edit question  →  /manage/exams/{id}/import/{index}/edit
                │     └─ Save    →  update session, redirect back to preview
                ├─ Remove        →  POST, removes from session, re-renders preview
                └─ Import All    →  POST /manage/exams/{id}/import/confirm
                      └─ Save all to DB → clear session → redirect to Questions page
```

An exam must exist before importing. There is no way to create an exam via import.

---

## JSON Format

The template contains one example of each question type.

```json
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
```

**Format rules:**
- Root must be a JSON array.
- `type` is case-insensitive. Accepted values: `MultipleChoice`, `Statement`, `DragAndDrop`.
- `allowMultipleAnswers` and `scoringMode` are optional; defaults are `false` and `"AllOrNothing"`.
- `scoringMode` accepted values: `"AllOrNothing"`, `"PartialCredit"` (case-insensitive).
- `pairs` is used for DragAndDrop; `options` is used for MultipleChoice and Statement.
- Unknown extra fields are ignored.

---

## Validation Rules

All errors are collected across all questions and shown together. Any error blocks the entire import — nothing is stored in session.

| Condition | Error message |
|-----------|--------------|
| File is not valid JSON | `Invalid JSON file` |
| Root is not an array | `File must contain a JSON array` |
| `type` missing or unrecognised | `Question N: unknown type "{value}"` |
| `text` empty or missing | `Question N: text is required` |
| MC/Statement: no options | `Question N: at least one option is required` |
| MC: no correct answer | `Question N: at least one correct answer is required` |
| MC: multiple correct but `allowMultipleAnswers` is false | `Question N: multiple correct answers require allowMultipleAnswers: true` |
| Statement: option text empty | `Question N, option M: text is required` |
| DragAndDrop: no pairs | `Question N: at least one pair is required` |
| DragAndDrop: pair missing label | `Question N, pair M: label is required` |
| DragAndDrop: pair missing sentence | `Question N, pair M: sentence is required` |

Questions and options are numbered from 1 in error messages.

---

## Import Preview Page

Route: `GET /manage/exams/{id}/import`

Shows the staged question list. If no staged questions exist in session for this exam, redirects to the Questions page.

Each row displays:
- Position index
- Type icon (☑ MultipleChoice, ⊙ Statement, ⇄ DragAndDrop)
- Question text (truncated with ellipsis if long)
- Metadata line: type label · option/pair count · scoring details if applicable
- **Edit** link → navigates to ImportEdit page
- **Remove** button → POST, removes from session, re-renders preview

Bottom of page:
- **Import N Questions** button → POST to confirm endpoint
- **Cancel** link → clears session staging for this exam, returns to Questions page

---

## Import Edit Page

Route: `GET /manage/exams/{id}/import/{index}/edit`

A direct clone of `QuestionForm.cshtml` — same section cards, same JS (type selector, scoring cards, correct toggles, DnD grid, image drop zone). The only difference is the data source and save target: reads from `session["import_{id}"][index]` and writes back to the same session slot on save.

The image section is **omitted** from the import editor. Images cannot be included in a JSON import; they must be added manually after import via the regular question editor.

On **Save**: updates the session slot and redirects to the preview list.
On **Cancel**: discards changes and redirects to the preview list.

---

## Architecture

### New files

| File | Responsibility |
|------|---------------|
| `Examageddon.Services/Models/StagedQuestion.cs` | `StagedQuestion` and `StagedOption` DTOs. These are what live in session. |
| `Examageddon.Services/QuestionImportService.cs` | `ParseAndValidate(json)` → staged list or errors. `ToQuestion(staged, examId, orderIndex)` → `Question` entity. `GetTemplateJson()` → template file contents. |
| `Examageddon.Web/Pages/Manage/Import.cshtml` + `.cs` | Preview list page. Handlers: `OnGetAsync`, `OnPostRemoveAsync`, `OnPostCancelAsync`, `OnPostConfirmAsync`. |
| `Examageddon.Web/Pages/Manage/ImportEdit.cshtml` + `.cs` | Staging edit page. Handlers: `OnGetAsync(int id, int index)`, `OnPostAsync(int id, int index)`. Same bound properties as `QuestionFormModel`, minus image. |

### Modified files

| File | Change |
|------|--------|
| `Examageddon.Web/Pages/Manage/Questions.cshtml` | Add **Download Template** button and inline **Import JSON** upload form (file input + submit). |
| `Examageddon.Web/Pages/Manage/Questions.cshtml.cs` | Add `OnGetTemplateAsync` (returns file result) and `OnPostUploadAsync(int id, IFormFile file)` (validate → session → redirect). |
| `Examageddon.Web/Program.cs` | Register `QuestionImportService` as scoped. |

### Question ordering

Imported questions are appended after any questions already in the exam. `OrderIndex` for each imported question is set to `existingQuestionCount + i` during confirm, preserving the order they appeared in the JSON file.

### Session storage

Key: `import_{examId}` (e.g. `import_3`)
Value: JSON-serialized `List<StagedQuestion>`

Written on successful upload validation. Updated on each edit or remove. Deleted on confirm or cancel.

### DTOs

```csharp
// Stored in session
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
    public string? MatchText { get; set; }  // populated for DragAndDrop pairs
}
```

The uploaded JSON uses a `pairs` array for DragAndDrop. `QuestionImportService.ParseAndValidate` normalises pairs into `StagedOption` with `MatchText` set, so the rest of the system (session, edit page, confirm) only deals with `StagedOption`.

---

## What Is Not In Scope

- Image import via JSON (images must be added manually after import)
- Creating an exam via import (exam must exist first)
- Exporting questions to JSON
- Merging imports with de-duplication logic
- Drag-to-reorder within the preview list
