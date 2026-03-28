# Active Session Gate — Design Spec

## Goal

When a user is identified (either already has a session cookie or selects a name on the home page), the app checks for an active (incomplete) exam session. If one exists, the user is redirected to a dedicated gate page where they must choose to continue or abandon the session before proceeding. They cannot bypass this choice through the normal UI.

## Architecture

Two trigger points in `IndexModel`, a dedicated gate page, two new service methods, and two new repository methods. No middleware.

### Trigger points

**`IndexModel.OnGetAsync`** — if `PersonId` is present in the ASP.NET Core session cookie, call `ExamSessionService.GetActiveSessionAsync(personId)`. If a session is found, immediately redirect to `/Sessions/Gate?sessionId={id}`. The person selection list never renders.

**`IndexModel.OnPostSelectAsync` and `OnPostNewAsync`** — after resolving the person via `PersonService.GetOrCreateAsync`, call `GetActiveSessionAsync(personId)`. If a session is found, redirect to the gate instead of `/Exams/Index`.

`IndexModel` currently only injects `PersonService`. `ExamSessionService` must be added as a second constructor parameter. `OnGetAsync` currently returns `Task` (void) — it must change to `Task<IActionResult>` so it can return a redirect; the non-redirect path must return `Page()`.

> **Out of scope:** the `/Exams/Index` page and `/Exams/Setup` page are not guarded. A user who navigates there directly via browser URL can bypass the gate. This is an accepted limitation of the chosen approach (Option C: gate at login only).

### Gate page: `Pages/Sessions/Gate`

The page uses the standard layout. The layout nav is present, but no other in-page actions exist — only Continue and Abandon. Antiforgery token handling follows the existing HTMX pattern in `_Layout.cshtml` (token already injected globally).

**`OnGetAsync(int sessionId)`**

1. If `PersonId` is not in the session cookie, redirect to `/`.
2. Load the session by `sessionId` (include `Exam`).
3. If not found, or `CompletedAt` is not null, or `session.PersonId != personId` — redirect to `/Exams/Index`.
4. Expose `session.Id` and `session.Exam.Title` to the view.

The ownership check (step 3) uses the `sessionId` query parameter to fetch, then validates the record belongs to the cookie's `PersonId`. This prevents one user abandoning another user's session.

**`OnPostContinueAsync(int sessionId)`**

1. Reload the session; if not found or already completed, redirect to `/Exams/Index`.
2. Redirect to `/Sessions/{sessionId}/Question/1`.

Redirecting to position 1 is intentional. For `FeedbackMode.AtEnd` the user can navigate freely from there. For `FeedbackMode.Direct`, answered questions display their locked feedback, so starting at position 1 is a valid resume point.

**`OnPostAbandonAsync(int sessionId)`**

1. Call `ExamSessionService.AbandonSessionAsync(sessionId, personId)` — passes the cookie `personId` for ownership verification.
2. Redirect to `/Exams/Index`.

## New Service Methods

**`GetActiveSessionAsync(int personId) → ExamSession?`**
Returns the most recent incomplete session for this person, or null.

**`AbandonSessionAsync(int sessionId, int personId)`**
Verifies the session belongs to `personId`, then delegates to the repository to hard-delete the session row. If the session does not exist or belongs to a different person, the method is a no-op. Related `SessionQuestion` and `SessionAnswer` rows are removed by the database cascade delete (`ON DELETE CASCADE` is configured in the schema by EF Core at `EnsureCreated` time; EF Core's SQLite provider enables `PRAGMA foreign_keys = ON` by default).

## New Repository Methods

**`GetActiveByPersonAsync(int personId) → ExamSession?`**
```csharp
return await db.ExamSessions
    .Include(s => s.Exam)
    .Where(s => s.PersonId == personId && s.CompletedAt == null)
    .OrderByDescending(s => s.StartedAt)
    .FirstOrDefaultAsync();
```
`OrderByDescending` ensures deterministic behaviour if multiple incomplete sessions exist for the same person (returns the most recently started one).

**`DeleteAsync(int sessionId)`**
```csharp
var session = await db.ExamSessions.FindAsync(sessionId);
if (session is not null)
{
    db.ExamSessions.Remove(session);
    await db.SaveChangesAsync();
}
```
This matches the existing pattern in `ExamRepository.DeleteAsync`. Cascade deletes are enforced at the SQLite schema level.

## Files

| Action | File |
|--------|------|
| Modify | `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs` |
| Modify | `src/Examageddon.Data/Repositories/ExamSessionRepository.cs` |
| Modify | `src/Examageddon.Services/ExamSessionService.cs` |
| Modify | `src/Examageddon.Web/Pages/Index.cshtml.cs` |
| Create | `src/Examageddon.Web/Pages/Sessions/Gate.cshtml` |
| Create | `src/Examageddon.Web/Pages/Sessions/Gate.cshtml.cs` |
| Modify | `src/Examageddon.Tests/ExamSessionServiceTests.cs` |

## Error / Edge Cases

- **Session completed between page load and POST**: gate `OnGetAsync` redirects to `/Exams/Index` if session is no longer active. Continue POST re-validates before redirecting.
- **Session belongs to a different person**: `OnGetAsync` validates ownership; treats mismatch as not found and redirects to `/Exams/Index`.
- **No person in cookie on gate page**: redirect to `/`.
- **Multiple incomplete sessions**: `GetActiveByPersonAsync` returns the most recently started one (ordered by `StartedAt DESC`).
- **Abandon a non-existent or already-deleted session**: `DeleteAsync` is a no-op if the row is not found; `AbandonSessionAsync` skips delete if ownership check fails.

## Testing

New test cases in `ExamSessionServiceTests`:

- `GetActiveSessionAsync_ReturnsSession_WhenIncompleteSessionExists`
- `GetActiveSessionAsync_ReturnsNull_WhenNoSessionExists`
- `GetActiveSessionAsync_ReturnsNull_WhenSessionIsCompleted`
- `GetActiveSessionAsync_ReturnsMostRecent_WhenMultipleIncompleteSessionsExist`
- `AbandonSessionAsync_DeletesSession_AndCascadesRelatedRows`
- `AbandonSessionAsync_IsNoOp_WhenSessionDoesNotExist`
- `AbandonSessionAsync_IsNoOp_WhenSessionBelongsToDifferentPerson`
