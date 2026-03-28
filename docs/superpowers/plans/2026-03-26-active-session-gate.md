# Active Session Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user is identified (cookie present or name selected), intercept any active incomplete exam session and force them to choose Continue or Abandon before proceeding.

**Architecture:** Two new repository methods, two new service methods, a new Gate Razor page, and targeted changes to `IndexModel`. No middleware. Gate checks live at two trigger points: `IndexModel.OnGetAsync` (already logged in) and the two POST handlers (just selected a name).

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 10, SQLite, XUnit, Tailwind CSS via CDN

---

## File Map

| File | Change |
|------|--------|
| `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs` | Add 2 method signatures |
| `src/Examageddon.Data/Repositories/ExamSessionRepository.cs` | Implement 2 new methods |
| `src/Examageddon.Services/ExamSessionService.cs` | Add 2 service methods |
| `src/Examageddon.Tests/ExamSessionServiceTests.cs` | Add 7 tests |
| `src/Examageddon.Web/Pages/Index.cshtml.cs` | Add DI, change OnGetAsync signature, add gate checks |
| `src/Examageddon.Web/Pages/Sessions/Gate.cshtml` | Create — gate view |
| `src/Examageddon.Web/Pages/Sessions/Gate.cshtml.cs` | Create — gate page model |

---

### Task 1: Repository layer — GetActiveByPersonAsync and DeleteAsync

**Files:**
- Modify: `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs`
- Modify: `src/Examageddon.Data/Repositories/ExamSessionRepository.cs`

No direct tests at the repository level — these are `internal sealed` and tested through the service in Task 2.

- [ ] **Step 1: Add the two method signatures to the interface**

Open `src/Examageddon.Data/Interfaces/IExamSessionRepository.cs`. Add after `SaveChangesAsync()`:

```csharp
Task<ExamSession?> GetActiveByPersonAsync(int personId);

Task DeleteAsync(int sessionId);
```

The complete file should look like:

```csharp
using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IExamSessionRepository
{
    Task<ExamSession> CreateAsync(ExamSession session, IReadOnlyList<SessionQuestion> sessionQuestions);

    Task<ExamSession?> GetByIdAsync(int sessionId);

    Task<SessionQuestion?> GetSessionQuestionAsync(int sessionId, int position);

    Task<SessionAnswer?> GetAnswerAsync(int sessionId, int questionId);

    Task<AnswerOption?> GetAnswerOptionAsync(int answerOptionId);

    Task AddAnswerAsync(SessionAnswer answer);

    Task<ExamSession?> GetWithExamAndAnswersAsync(int sessionId);

    Task<ExamSession?> GetFullSessionAsync(int sessionId);

    Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId);

    Task SaveChangesAsync();

    Task<ExamSession?> GetActiveByPersonAsync(int personId);

    Task DeleteAsync(int sessionId);
}
```

- [ ] **Step 2: Implement both methods in the repository**

Open `src/Examageddon.Data/Repositories/ExamSessionRepository.cs`. Add at the end of the class, before the closing `}`:

```csharp
public Task<ExamSession?> GetActiveByPersonAsync(int personId)
{
    return db.ExamSessions
        .Include(s => s.Exam)
        .Where(s => s.PersonId == personId && s.CompletedAt == null)
        .OrderByDescending(s => s.StartedAt)
        .FirstOrDefaultAsync();
}

public async Task DeleteAsync(int sessionId)
{
    var session = await db.ExamSessions.FindAsync(sessionId);
    if (session is not null)
    {
        db.ExamSessions.Remove(session);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

```
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```
git add src/Examageddon.Data/Interfaces/IExamSessionRepository.cs
git add src/Examageddon.Data/Repositories/ExamSessionRepository.cs
git commit -m  # Show diff to user and wait for explicit approval before committing "feat: add GetActiveByPersonAsync and DeleteAsync to session repository"
```

---

### Task 2: Service layer — GetActiveSessionAsync and AbandonSessionAsync, with tests

**Files:**
- Modify: `src/Examageddon.Services/ExamSessionService.cs`
- Modify: `src/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 1: Write the 7 failing tests**

Open `src/Examageddon.Tests/ExamSessionServiceTests.cs`. Add the following test methods **before** the `SeedAsync` private method at the bottom of the class:

```csharp
[Fact]
public async Task GetActiveSessionAsyncReturnsSessionWhenIncompleteSessionExists()
{
    var (ctx, person, exam) = await SeedAsync(2);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

    var result = await BuildService(ctx).GetActiveSessionAsync(person.Id);

    Assert.NotNull(result);
    Assert.Equal(person.Id, result.PersonId);
    Assert.Null(result.CompletedAt);
}

[Fact]
public async Task GetActiveSessionAsyncReturnsNullWhenNoSessionExists()
{
    var (ctx, person, _) = await SeedAsync(2);

    var result = await BuildService(ctx).GetActiveSessionAsync(person.Id);

    Assert.Null(result);
}

[Fact]
public async Task GetActiveSessionAsyncReturnsNullWhenSessionIsCompleted()
{
    var (ctx, person, exam) = await SeedAsync(2);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
    var svc = BuildService(ctx);
    var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
    await svc.CompleteSessionAsync(session.Id);

    var result = await svc.GetActiveSessionAsync(person.Id);

    Assert.Null(result);
}

[Fact]
public async Task GetActiveSessionAsyncReturnsMostRecentWhenMultipleIncompleteSessionsExist()
{
    var (ctx, person, exam) = await SeedAsync(2);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    var svc = BuildService(ctx);
    var first = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
    var second = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

    // Force first to appear older
    first.StartedAt = DateTime.UtcNow.AddMinutes(-5);
    await ctx.SaveChangesAsync();

    var result = await svc.GetActiveSessionAsync(person.Id);

    Assert.NotNull(result);
    Assert.Equal(second.Id, result.Id);
}

[Fact]
public async Task AbandonSessionAsyncDeletesSessionAndCascadesRelatedRows()
{
    var (ctx, person, exam) = await SeedAsync(2);
    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);
    var sessionId = session.Id;

    await BuildService(ctx).AbandonSessionAsync(sessionId, person.Id);

    Assert.Null(await ctx.ExamSessions.FindAsync(sessionId));
    Assert.Empty(ctx.SessionQuestions.Where(sq => sq.SessionId == sessionId));
}

[Fact]
public async Task AbandonSessionAsyncIsNoOpWhenSessionDoesNotExist()
{
    var (ctx, person, _) = await SeedAsync(2);

    // Should not throw
    await BuildService(ctx).AbandonSessionAsync(99999, person.Id);
}

[Fact]
public async Task AbandonSessionAsyncIsNoOpWhenSessionBelongsToDifferentPerson()
{
    var (ctx, person, exam) = await SeedAsync(2);
    var otherPerson = new Person { Name = "Other" };
    ctx.Persons.Add(otherPerson);
    await ctx.SaveChangesAsync();

    var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
    var session = await BuildService(ctx).CreateSessionAsync(person.Id, exam.Id, opts);

    // Other person tries to abandon person's session
    await BuildService(ctx).AbandonSessionAsync(session.Id, otherPerson.Id);

    // Session must still exist
    Assert.NotNull(await ctx.ExamSessions.FindAsync(session.Id));
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

```
dotnet test src/Examageddon.Tests/ --filter "ExamSessionServiceTests"
```

Expected: **Build error** — `ExamSessionService` does not yet have `GetActiveSessionAsync` or `AbandonSessionAsync`, so the project will not compile. This is the expected red state in TDD.

- [ ] **Step 3: Add the two service methods**

Open `src/Examageddon.Services/ExamSessionService.cs`. Add at the end of the class, before the closing `}`:

```csharp
public Task<ExamSession?> GetActiveSessionAsync(int personId)
{
    return sessionRepo.GetActiveByPersonAsync(personId);
}

public async Task AbandonSessionAsync(int sessionId, int personId)
{
    var session = await sessionRepo.GetActiveByPersonAsync(personId);
    if (session is null || session.Id != sessionId)
    {
        return;
    }

    await sessionRepo.DeleteAsync(sessionId);
}
```

- [ ] **Step 4: Run the tests to confirm they all pass**

```
dotnet test src/Examageddon.Tests/ --filter "ExamSessionServiceTests"
```

Expected: All 12 tests PASS (5 existing + 7 new).

- [ ] **Step 5: Commit**

```
git add src/Examageddon.Services/ExamSessionService.cs
git add src/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m  # Show diff to user and wait for explicit approval before committing "feat: add GetActiveSessionAsync and AbandonSessionAsync to session service"
```

---

### Task 3: Gate Razor page

**Files:**
- Create: `src/Examageddon.Web/Pages/Sessions/Gate.cshtml.cs`
- Create: `src/Examageddon.Web/Pages/Sessions/Gate.cshtml`

- [ ] **Step 1: Create the page model**

Create `src/Examageddon.Web/Pages/Sessions/Gate.cshtml.cs`:

```csharp
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class GateModel(ExamSessionService sessionService) : PageModel
{
    public string ExamTitle { get; set; } = string.Empty;

    public int SessionId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        var session = await sessionService.GetActiveSessionAsync(personId.Value);
        if (session is null)
        {
            return RedirectToPage("/Exams/Index");
        }

        ExamTitle = session.Exam.Title;
        SessionId = session.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostContinueAsync(int sessionId)
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        var session = await sessionService.GetActiveSessionAsync(personId.Value);
        if (session is null || session.Id != sessionId)
        {
            return RedirectToPage("/Exams/Index");
        }

        return RedirectToPage("/Sessions/Question", new { sessionId, n = 1 });
    }

    public async Task<IActionResult> OnPostAbandonAsync(int sessionId)
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        await sessionService.AbandonSessionAsync(sessionId, personId.Value);
        return RedirectToPage("/Exams/Index");
    }
}
```

- [ ] **Step 2: Create the Razor view**

Create `src/Examageddon.Web/Pages/Sessions/Gate.cshtml`:

```razor
@page "/sessions/gate"
@model GateModel
@{ ViewData["Title"] = "Active Session"; }

<div class="text-center mb-10">
    <h1 class="text-3xl font-black tracking-tight mb-2">You have an active session</h1>
    <p class="text-arcade-muted">You were in the middle of <span class="text-arcade-text font-semibold">@Model.ExamTitle</span></p>
</div>

<div class="bg-arcade-card border border-arcade-border rounded-xl p-8 max-w-md mx-auto">
    <p class="text-arcade-muted text-sm text-center mb-6">
        What would you like to do?
    </p>

    <div class="flex flex-col gap-3">
        <form method="post" asp-page-handler="Continue">
            <input type="hidden" name="sessionId" value="@Model.SessionId" />
            <button type="submit"
                class="w-full bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold px-6 py-3 rounded-xl hover:opacity-90 transition">
                Continue session
            </button>
        </form>

        <form method="post" asp-page-handler="Abandon">
            <input type="hidden" name="sessionId" value="@Model.SessionId" />
            <button type="submit"
                class="w-full bg-arcade-card border border-red-800 text-red-400 font-semibold px-6 py-3 rounded-xl hover:bg-red-950 transition">
                Abandon session
            </button>
        </form>
    </div>
</div>
```

- [ ] **Step 3: Build to verify no compile errors**

```
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```
git add src/Examageddon.Web/Pages/Sessions/Gate.cshtml
git add src/Examageddon.Web/Pages/Sessions/Gate.cshtml.cs
git commit -m  # Show diff to user and wait for explicit approval before committing "feat: add active session gate page"
```

---

### Task 4: Wire up gate checks in IndexModel

**Files:**
- Modify: `src/Examageddon.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Update IndexModel**

Replace the entire contents of `src/Examageddon.Web/Pages/Index.cshtml.cs` with:

```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages;

public class IndexModel(PersonService personService, ExamSessionService sessionService) : PageModel
{
    public List<Person> People { get; set; } = [];

    [BindProperty]
    public string NewName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is not null)
        {
            var active = await sessionService.GetActiveSessionAsync(personId.Value);
            if (active is not null)
            {
                return RedirectToPage("/Sessions/Gate", new { sessionId = active.Id });
            }
        }

        People = await personService.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(string name)
    {
        var person = await personService.GetOrCreateAsync(name);
        HttpContext.Session.SetInt32(SessionKeys.PersonId, person.Id);
        HttpContext.Session.SetString(SessionKeys.PersonName, person.Name);

        var active = await sessionService.GetActiveSessionAsync(person.Id);
        if (active is not null)
        {
            return RedirectToPage("/Sessions/Gate", new { sessionId = active.Id });
        }

        return RedirectToPage("/Exams/Index");
    }

    public async Task<IActionResult> OnPostNewAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            return RedirectToPage();
        }

        var person = await personService.GetOrCreateAsync(NewName.Trim());
        HttpContext.Session.SetInt32(SessionKeys.PersonId, person.Id);
        HttpContext.Session.SetString(SessionKeys.PersonName, person.Name);

        var active = await sessionService.GetActiveSessionAsync(person.Id);
        if (active is not null)
        {
            return RedirectToPage("/Sessions/Gate", new { sessionId = active.Id });
        }

        return RedirectToPage("/Exams/Index");
    }
}
```

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Examageddon.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Run all tests to confirm nothing broke**

```
dotnet test src/Examageddon.Tests/
```

Expected: All 15+ tests PASS.

- [ ] **Step 4: Commit**

```
git add src/Examageddon.Web/Pages/Index.cshtml.cs
git commit -m  # Show diff to user and wait for explicit approval before committing "feat: redirect to gate on home page if active session exists"
```

---

## Manual smoke test

After all tasks are complete:

1. Run the app: `dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj`
2. Select a user and start an exam session.
3. Mid-session, navigate back to `/` (the home page).
4. **Expected:** Redirected immediately to the gate page showing the exam title with two buttons.
5. Click **Continue** — should resume at question 1 of the active session.
6. Start another session, go back to `/`, click **Abandon** — should land on `/Exams/Index`; the session row should be gone from the database.
7. Select a different user who has no active session — should go straight to `/Exams/Index`.
