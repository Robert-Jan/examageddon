# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Superpowers

**Always ask before using superpowers.** When the user requests a change, ask first: "Do you want me to use superpowers (brainstorm → spec → plan → subagent execution) or just implement it directly?" Do not invoke brainstorming, writing-plans, or subagent-driven-development unless the user confirms they want the full superpowers workflow.

## Git

**Commit message format:** `<type>(<scope>): <subject>`

Types:
- `feat` — A new feature
- `fix` — A bug fix
- `docs` — Documentation only changes
- `style` — Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
- `refactor` — A code change that neither fixes a bug nor adds a feature
- `perf` — A code change that improves performance
- `test` — Adding missing tests
- `chore` — Changes to the build process or auxiliary tools and libraries such as documentation generation

**Never commit automatically.** Always show the changes and wait for the user to review and explicitly approve before running `git commit`. Do not include `Co-Authored-By` trailers in commit messages.

**Never commit during sub-agent runs.** In superpowers mode, hold all commits until the full plan is complete and the user is back in control.

## Commands
When running commands execute them after each other to make better use of the allowed commands list in the Claude settings. Only use compound (&&) commands if is is absoulutly nessesery!

```bash
# Build entire solution
dotnet build src/Examageddon.slnx

# Run the web app
dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj

# Run all tests
dotnet test src/Examageddon.Tests/

# Run a single test class
dotnet test src/Examageddon.Tests/ --filter "ExamSessionServiceTests"

# Run a single test
dotnet test src/Examageddon.Tests/ --filter "ExamSessionServiceTests.CompleteSession_CalculatesPassFail"
```

## Architecture

5 projects all under `src/`:

- **Examageddon.Data** — EF Core entities, enums, `ExamageddonDbContext`. SQLite database created via `EnsureCreated()` on startup (no migrations CLI needed in dev). Contains `Interfaces/` (public repository interfaces) and `Repositories/` (internal sealed implementations). Register via `services.AddRepositories()`.
- **Examageddon.Services** — Business logic. `PersonService`, `ExamManagementService`, `ExamSessionService`, `HistoryService`. DTOs live in `Models/`. Services depend on repository interfaces, not `DbContext` directly. No web dependencies.
- **Examageddon.Web** — Razor Pages + HTMX. Tailwind CSS and HTMX loaded via CDN (no build step). Session cookie holds `PersonId` (int) and `PersonName` (string) via `SessionKeys` constants. Image blobs served via minimal API endpoint at `/images/question/{id}`.
- **Examageddon.Tests** — XUnit. Uses SQLite `:memory:` via `TestDbContextFactory`. Instantiates internal repository classes directly (allowed via `InternalsVisibleTo`). Tests cover all services.

### Key flows

**Exam session:** `POST /exams/{id}/setup` → creates `ExamSession` + `SessionQuestion` rows → redirects to `/sessions/{id}/question/1`. Each answer POSTs via HTMX and returns a partial (`_AnswerFeedback.cshtml`) that replaces `#answer-container`.

**Feedback modes:** Direct = answer locks immediately with green/red feedback, Next button injected via HTMX. AtEnd = free Back/Next navigation, Review page before submit.

**HTMX antiforgery:** `@Html.AntiForgeryToken()` is rendered in `_Layout.cshtml`; a `htmx:configRequest` listener injects the token as a header on every HTMX request.

**Database:** SQLite file at `database/examageddon.db`. Created automatically on first run.

### Important service notes

- `ExamManagementService.GetAllExamsAsync()` does NOT include questions (avoids loading image blobs). Use `GetQuestionCountsAsync()` for question counts in list views.
- `ExamManagementService.GetExamAsync(id)` includes Questions + AnswerOptions (used for single-exam detail pages).
- `ExamSessionService` takes both `IExamRepository` and `IExamSessionRepository` — it needs exam data to build sessions.
- `ExamSessionService.GetSessionQuestionAsync(sessionId, n, fromReview)` loads the question with eager-loaded answer options and existing answer.
