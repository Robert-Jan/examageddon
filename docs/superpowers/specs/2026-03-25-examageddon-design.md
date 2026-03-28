# Examageddon — Design Spec

**Date:** 2026-03-25
**Stack:** .NET 10 · Razor Pages · HTMX · Tailwind CSS (dev CDN) · SQLite (EF Core)

---

## Overview

Examageddon is a kiosk-style exam practice tool. Users select their name at the start of each session (no authentication). Managers can freely create exams with multiple-choice questions (including image attachments). Students take exams with configurable question count, ordering, and feedback mode. All progress is persisted to SQLite in real time.

---

## Identity Model

No accounts or authentication. On first visit (or via a persistent cookie), users are presented with a **name picker** page. They select an existing name from a list or type a new one. The selected name is stored in a session cookie and used to associate all exam attempts. Names are stored in the `Person` table.

---

## UI Style

**Dark Arcade.** Dark background (`#0f172a` / `#1e293b`), amber/red gradient accents (`#f59e0b` → `#ef4444`), high contrast text (`#f1f5f9`). Playful energy bar / streak feel. Tailwind CSS via dev CDN (no build step). HTMX via CDN.

---

## Solution Structure

```
Examageddon.sln
├── src/
│   ├── Examageddon.Data/         — EF Core entities, DbContext, migrations
│   ├── Examageddon.Services/     — Business logic services
│   └── Examageddon.Web/          — Razor Pages, HTMX partials, image endpoint
└── tests/
    └── Examageddon.Tests/        — XUnit tests, in-memory SQLite
```

**Project references:** `Web` → `Services` → `Data`

---

## Data Model

### `Person`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Name | string | Unique |

### `Exam`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Title | string | |
| Description | string | nullable |
| PassingScorePercent | int | 0–100 |
| ExamQuestionCount | int | # questions in "real exam" mode |

### `Question`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| ExamId | int FK | |
| Text | string | |
| QuestionType | enum | MultipleChoice (extensible) |
| ImageData | byte[] | nullable, stored as blob in SQLite |
| ImageContentType | string | nullable (e.g. "image/png") |
| OrderIndex | int | default ordering |

### `AnswerOption`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| QuestionId | int FK | |
| Text | string | |
| IsCorrect | bool | |
| OrderIndex | int | |

### `ExamSession`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| PersonId | int FK | |
| ExamId | int FK | |
| StartedAt | DateTime | |
| CompletedAt | DateTime? | null until submitted |
| QuestionMode | enum | All \| Limited (count taken from `Exam.ExamQuestionCount`) |
| OrderMode | enum | Default \| Random |
| FeedbackMode | enum | Direct \| AtEnd |
| TotalQuestions | int | snapshot at session creation |
| CorrectAnswers | int | updated on completion |
| IsPassed | bool? | null until completed |

### `SessionQuestion`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| SessionId | int FK | |
| QuestionId | int FK | |
| Position | int | 1-based display order for this session |

### `SessionAnswer`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| SessionId | int FK | |
| QuestionId | int FK | |
| SelectedAnswerOptionId | int FK | |
| IsCorrect | bool | |
| AnsweredAt | DateTime | |

Answers are written to the database immediately when the student submits each question — no buffering in memory.

---

## Services

### `PersonService`
- `GetAllAsync()` — list for name picker
- `GetOrCreateAsync(name)` — returns existing or creates new `Person`

### `ExamManagementService`
- Full CRUD for `Exam` and `Question`
- `GetImageAsync(questionId)` — returns `(byte[] data, string contentType)`
- `SaveImageAsync(questionId, data, contentType)`

### `ExamSessionService`
- `CreateSessionAsync(personId, examId, options)` — creates `ExamSession`, picks questions (all or limited), orders them (default or random), writes `SessionQuestion` rows
- `GetSessionQuestionAsync(sessionId, position)` — returns question + options + existing answer if any
- `SubmitAnswerAsync(sessionId, questionId, answerOptionId)` — writes/updates `SessionAnswer` with `IsCorrect`
- `CompleteSessionAsync(sessionId)` — calculates `CorrectAnswers`, sets `IsPassed`, sets `CompletedAt`
- `GetResultAsync(sessionId)` — returns score, pass/fail, per-question breakdown

### `HistoryService`
- `GetHistoryForPersonAsync(personId)` — returns all completed `ExamSession` rows with exam title, score, pass/fail, date

---

## Pages & Routing

### Exam Flow

| Route | Page | Description |
|---|---|---|
| `GET /` | Name Picker | Select or create name; stored in session cookie |
| `GET /exams` | Exam Selector | Cards per exam: title, # questions, passing % |
| `GET /exams/{id}/setup` | Session Setup | Choose: all vs limited, default vs random, direct vs end feedback |
| `POST /exams/{id}/setup` | — | Creates `ExamSession` + `SessionQuestion` rows → redirect to question 1 |
| `GET /sessions/{id}/question/{n}` | Question Page | Single question; HTMX answer submission |
| `GET /sessions/{id}/review` | Review Page | End-mode only; all answers, jump-to-question links, Submit button |
| `POST /sessions/{id}/complete` | — | Completes session → redirect to result |
| `GET /sessions/{id}/result` | Result Page | Pass/fail, score, retry/home options |
| `GET /history` | My History | All past attempts for current person |

### Management Flow

| Route | Page | Description |
|---|---|---|
| `GET /manage` | Exam List | All exams; edit/delete; New Exam button |
| `GET /manage/exams/create` | Create Exam | Title, description, passing %, question count |
| `POST /manage/exams/create` | — | Creates exam → redirect to question list |
| `GET /manage/exams/{id}/edit` | Edit Exam | Same form, pre-filled |
| `POST /manage/exams/{id}/edit` | — | Updates exam |
| `GET /manage/exams/{id}/questions` | Question List | All questions; reorder; add/delete |
| `GET /manage/exams/{id}/questions/add` | Add Question | Text, image upload, answer options, mark correct |
| `POST /manage/exams/{id}/questions/add` | — | Saves question + options + image |
| `GET /manage/exams/{id}/questions/{qid}/edit` | Edit Question | Pre-filled form |
| `POST /manage/exams/{id}/questions/{qid}/edit` | — | Updates question |

### Image Endpoint
`GET /images/question/{id}` — streams `ImageData` from SQLite with correct `Content-Type` header.

---

## Exam Session Flow Detail

### Direct Feedback Mode
1. Question shown; no Next/Back buttons
2. Student clicks an answer → `hx-post` to submit
3. Server returns partial: selected option turns green ✓ or red ✗, other options dim, answer locked, **Next →** button injected
4. On last question: **Next** replaced by **Finish 🏁** → redirects to Result page (no Review step)

### Feedback at End Mode
1. Question shown with **← Back** and **Next →** buttons
2. Student clicks an answer → `hx-post` to submit (no color feedback, just highlight selection)
3. Student can change answer by clicking another option (new POST overwrites `SessionAnswer`)
4. Navigation is free — Back/Next move between questions
5. On last question: **Next** replaced by **Review →**
6. Review page: table of all questions + selected answers + unanswered flags; click any row → jump back to that question. When returning to a question from the Review page, a **"← Back to Review"** button replaces the normal Back/Next buttons so the student returns directly to the Review page after changing their answer.
7. **Submit** → `POST /sessions/{id}/complete` → Result page

**Unanswered questions** in end mode: flagged on the Review page; count as incorrect on submission.

---

## HTMX Interactions

| Trigger | Target | Action |
|---|---|---|
| Click answer option | Answer list container | POST answer; return partial with feedback (direct) or updated selection (end) |
| Change file input (image) | Preview container | Client-side FileReader preview only (no server round-trip); image is persisted when the question form is submitted |
| Click delete (question/exam) | Table row | `hx-delete` + `hx-confirm`; row removed on success |

All other interactions use standard Razor Page form POST + PRG redirect.

---

## Testing

**Project:** `Examageddon.Tests` (XUnit)

- `ExamSessionService` tests: session creation with all/limited question modes, default/random ordering, answer submission, score calculation, pass/fail determination
- `ExamManagementService` tests: create/update/delete exam and question, image round-trip
- `PersonService` tests: get-or-create idempotency
- Data layer uses SQLite `:memory:` database (not `UseInMemoryDatabase` — SQLite in-memory preserves relational constraints)

---

## Non-Goals (v1)

- Authentication / user accounts
- Multiple correct answers per question
- Other question types beyond multiple choice
- Leaderboard / cross-user comparison
- Export / import of exams
