# Examageddon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a kiosk-style exam practice web app with management, exam-taking, and history features.

**Architecture:** 4-project .NET 10 solution (Data → Services → Web, Tests). Razor Pages + HTMX for interaction, Tailwind CSS play CDN, SQLite via EF Core for all persistence including image blobs.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, HTMX 2.x (CDN), Tailwind CSS Play CDN, EF Core 10, SQLite, XUnit

---

## File Map

```
src/Examageddon.Data/
  Examageddon.Data.csproj
  ExamageddonDbContext.cs
  Entities/Person.cs
  Entities/Exam.cs
  Entities/Question.cs
  Entities/AnswerOption.cs
  Entities/ExamSession.cs
  Entities/SessionQuestion.cs
  Entities/SessionAnswer.cs
  Enums/QuestionType.cs
  Enums/QuestionMode.cs
  Enums/OrderMode.cs
  Enums/FeedbackMode.cs

src/Examageddon.Services/
  Examageddon.Services.csproj
  Models/SessionOptions.cs
  Models/SessionQuestionModel.cs
  Models/SessionResultModel.cs
  PersonService.cs
  ExamManagementService.cs
  ExamSessionService.cs
  HistoryService.cs

src/Examageddon.Web/
  Examageddon.Web.csproj
  Program.cs
  Pages/
    _ViewImports.cshtml
    _ViewStart.cshtml
    Shared/_Layout.cshtml
    Index.cshtml + .cshtml.cs          (name picker)
    Exams/Index.cshtml + .cs           (exam selector)
    Exams/Setup.cshtml + .cs           (session setup)
    Sessions/Question.cshtml + .cs     (question page)
    Sessions/Review.cshtml + .cs       (review page)
    Sessions/Result.cshtml + .cs       (result page)
    History/Index.cshtml + .cs
    Manage/Index.cshtml + .cs          (exam list)
    Manage/ExamForm.cshtml + .cs       (create/edit exam)
    Manage/Questions.cshtml + .cs      (question list)
    Manage/QuestionForm.cshtml + .cs   (add/edit question)
  Endpoints/ImageEndpoint.cs

tests/Examageddon.Tests/
  Examageddon.Tests.csproj
  Helpers/TestDbContextFactory.cs
  PersonServiceTests.cs
  ExamManagementServiceTests.cs
  ExamSessionServiceTests.cs
```

---

## Task 1: Scaffold Solution & Projects

**Files:**
- Create: `Examageddon.sln`
- Create: `src/Examageddon.Data/Examageddon.Data.csproj`
- Create: `src/Examageddon.Services/Examageddon.Services.csproj`
- Create: `src/Examageddon.Web/Examageddon.Web.csproj`
- Create: `tests/Examageddon.Tests/Examageddon.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd "C:/Code/Boskalis/Playground/examageddon"
dotnet new sln -n Examageddon
dotnet new classlib -n Examageddon.Data -f net10.0 -o src/Examageddon.Data
dotnet new classlib -n Examageddon.Services -f net10.0 -o src/Examageddon.Services
dotnet new web -n Examageddon.Web -f net10.0 -o src/Examageddon.Web
dotnet new xunit -n Examageddon.Tests -f net10.0 -o tests/Examageddon.Tests
```

- [ ] **Step 2: Add projects to solution**

```bash
dotnet sln add src/Examageddon.Data/Examageddon.Data.csproj
dotnet sln add src/Examageddon.Services/Examageddon.Services.csproj
dotnet sln add src/Examageddon.Web/Examageddon.Web.csproj
dotnet sln add tests/Examageddon.Tests/Examageddon.Tests.csproj
```

- [ ] **Step 3: Add project references**

```bash
dotnet add src/Examageddon.Services/Examageddon.Services.csproj reference src/Examageddon.Data/Examageddon.Data.csproj
dotnet add src/Examageddon.Web/Examageddon.Web.csproj reference src/Examageddon.Services/Examageddon.Services.csproj
dotnet add tests/Examageddon.Tests/Examageddon.Tests.csproj reference src/Examageddon.Services/Examageddon.Services.csproj
dotnet add tests/Examageddon.Tests/Examageddon.Tests.csproj reference src/Examageddon.Data/Examageddon.Data.csproj
```

- [ ] **Step 4: Add NuGet packages**

```bash
dotnet add src/Examageddon.Data/Examageddon.Data.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Examageddon.Data/Examageddon.Data.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/Examageddon.Web/Examageddon.Web.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Examageddon.Web/Examageddon.Web.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add tests/Examageddon.Tests/Examageddon.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

- [ ] **Step 5: Delete generated boilerplate**

```bash
rm src/Examageddon.Data/Class1.cs
rm src/Examageddon.Services/Class1.cs
```

- [ ] **Step 6: Verify solution builds**

```bash
dotnet build Examageddon.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git init
git add .
git commit -m "feat: scaffold solution with Data, Services, Web, Tests projects"
```

---

## Task 2: Data Entities & Enums

**Files:**
- Create: `src/Examageddon.Data/Enums/QuestionType.cs`
- Create: `src/Examageddon.Data/Enums/QuestionMode.cs`
- Create: `src/Examageddon.Data/Enums/OrderMode.cs`
- Create: `src/Examageddon.Data/Enums/FeedbackMode.cs`
- Create: `src/Examageddon.Data/Entities/Person.cs`
- Create: `src/Examageddon.Data/Entities/Exam.cs`
- Create: `src/Examageddon.Data/Entities/Question.cs`
- Create: `src/Examageddon.Data/Entities/AnswerOption.cs`
- Create: `src/Examageddon.Data/Entities/ExamSession.cs`
- Create: `src/Examageddon.Data/Entities/SessionQuestion.cs`
- Create: `src/Examageddon.Data/Entities/SessionAnswer.cs`

- [ ] **Step 1: Create enums**

`src/Examageddon.Data/Enums/QuestionType.cs`:
```csharp
namespace Examageddon.Data.Enums;
public enum QuestionType { MultipleChoice }
```

`src/Examageddon.Data/Enums/QuestionMode.cs`:
```csharp
namespace Examageddon.Data.Enums;
public enum QuestionMode { All, Limited }
```

`src/Examageddon.Data/Enums/OrderMode.cs`:
```csharp
namespace Examageddon.Data.Enums;
public enum OrderMode { Default, Random }
```

`src/Examageddon.Data/Enums/FeedbackMode.cs`:
```csharp
namespace Examageddon.Data.Enums;
public enum FeedbackMode { Direct, AtEnd }
```

- [ ] **Step 2: Create entity classes**

`src/Examageddon.Data/Entities/Person.cs`:
```csharp
namespace Examageddon.Data.Entities;
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ExamSession> Sessions { get; set; } = [];
}
```

`src/Examageddon.Data/Entities/Exam.cs`:
```csharp
namespace Examageddon.Data.Entities;
public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PassingScorePercent { get; set; }
    public int ExamQuestionCount { get; set; }
    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<ExamSession> Sessions { get; set; } = [];
}
```

`src/Examageddon.Data/Entities/Question.cs`:
```csharp
using Examageddon.Data.Enums;
namespace Examageddon.Data.Entities;
public class Question
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public int OrderIndex { get; set; }
    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];
}
```

`src/Examageddon.Data/Entities/AnswerOption.cs`:
```csharp
namespace Examageddon.Data.Entities;
public class AnswerOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}
```

`src/Examageddon.Data/Entities/ExamSession.cs`:
```csharp
using Examageddon.Data.Enums;
namespace Examageddon.Data.Entities;
public class ExamSession
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public QuestionMode QuestionMode { get; set; }
    public OrderMode OrderMode { get; set; }
    public FeedbackMode FeedbackMode { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public bool? IsPassed { get; set; }
    public ICollection<SessionQuestion> SessionQuestions { get; set; } = [];
    public ICollection<SessionAnswer> SessionAnswers { get; set; } = [];
}
```

`src/Examageddon.Data/Entities/SessionQuestion.cs`:
```csharp
namespace Examageddon.Data.Entities;
public class SessionQuestion
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ExamSession Session { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Position { get; set; }
}
```

`src/Examageddon.Data/Entities/SessionAnswer.cs`:
```csharp
namespace Examageddon.Data.Entities;
public class SessionAnswer
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ExamSession Session { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int SelectedAnswerOptionId { get; set; }
    public AnswerOption SelectedAnswerOption { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/Examageddon.Data/Examageddon.Data.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Data/
git commit -m "feat: add data entities and enums"
```

---

## Task 3: DbContext & Database Setup

**Files:**
- Create: `src/Examageddon.Data/ExamageddonDbContext.cs`

- [ ] **Step 1: Create DbContext**

`src/Examageddon.Data/ExamageddonDbContext.cs`:
```csharp
using Examageddon.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data;

public class ExamageddonDbContext(DbContextOptions<ExamageddonDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();
    public DbSet<SessionAnswer> SessionAnswers => Set<SessionAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>()
            .HasIndex(p => p.Name).IsUnique();

        modelBuilder.Entity<SessionAnswer>()
            .HasIndex(sa => new { sa.SessionId, sa.QuestionId }).IsUnique();
    }
}
```

- [ ] **Step 2: Create test DbContext factory**

`tests/Examageddon.Tests/Helpers/TestDbContextFactory.cs`:
```csharp
using Examageddon.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ExamageddonDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ExamageddonDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new ExamageddonDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
```

- [ ] **Step 3: Verify test project builds**

```bash
dotnet build tests/Examageddon.Tests/Examageddon.Tests.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Data/ tests/Examageddon.Tests/
git commit -m "feat: add DbContext and test factory"
```

---

## Task 4: Service Models

**Files:**
- Create: `src/Examageddon.Services/Models/SessionOptions.cs`
- Create: `src/Examageddon.Services/Models/SessionQuestionModel.cs`
- Create: `src/Examageddon.Services/Models/SessionResultModel.cs`
- Create: `src/Examageddon.Services/Models/HistoryEntryModel.cs`

- [ ] **Step 1: Create DTOs**

`src/Examageddon.Services/Models/SessionOptions.cs`:
```csharp
using Examageddon.Data.Enums;
namespace Examageddon.Services.Models;
public class SessionOptions
{
    public QuestionMode QuestionMode { get; set; }
    public OrderMode OrderMode { get; set; }
    public FeedbackMode FeedbackMode { get; set; }
}
```

`src/Examageddon.Services/Models/SessionQuestionModel.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
namespace Examageddon.Services.Models;
public class SessionQuestionModel
{
    public int SessionId { get; set; }
    public int Position { get; set; }
    public int TotalQuestions { get; set; }
    public FeedbackMode FeedbackMode { get; set; }
    public Question Question { get; set; } = null!;
    public List<AnswerOption> AnswerOptions { get; set; } = [];
    public int? ExistingAnswerOptionId { get; set; }
    public bool IsAnswered => ExistingAnswerOptionId.HasValue;
    public bool IsFromReview { get; set; }
}
```

`src/Examageddon.Services/Models/SessionResultModel.cs`:
```csharp
using Examageddon.Data.Entities;
namespace Examageddon.Services.Models;
public class SessionResultModel
{
    public int SessionId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int PassingScorePercent { get; set; }
    public bool IsPassed { get; set; }
    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round((double)CorrectAnswers / TotalQuestions * 100, 1);
    public List<QuestionResultItem> QuestionResults { get; set; } = [];
}
public class QuestionResultItem
{
    public Question Question { get; set; } = null!;
    public AnswerOption? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsAnswered { get; set; }
}
```

`src/Examageddon.Services/Models/HistoryEntryModel.cs`:
```csharp
namespace Examageddon.Services.Models;
public class HistoryEntryModel
{
    public int SessionId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public double ScorePercent => TotalQuestions == 0 ? 0 : Math.Round((double)CorrectAnswers / TotalQuestions * 100, 1);
    public bool IsPassed { get; set; }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Examageddon.Services/Examageddon.Services.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Services/
git commit -m "feat: add service DTOs"
```

---

## Task 5: PersonService (TDD)

**Files:**
- Create: `src/Examageddon.Services/PersonService.cs`
- Create: `tests/Examageddon.Tests/PersonServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/Examageddon.Tests/PersonServiceTests.cs`:
```csharp
using Examageddon.Services;
using Examageddon.Tests.Helpers;
using Xunit;

namespace Examageddon.Tests;

public class PersonServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAllPersons()
    {
        using var ctx = TestDbContextFactory.Create();
        ctx.Persons.AddRange(
            new Data.Entities.Person { Name = "Alice" },
            new Data.Entities.Person { Name = "Bob" });
        await ctx.SaveChangesAsync();

        var svc = new PersonService(ctx);
        var result = await svc.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesNewPerson()
    {
        using var ctx = TestDbContextFactory.Create();
        var svc = new PersonService(ctx);

        var person = await svc.GetOrCreateAsync("Charlie");

        Assert.NotEqual(0, person.Id);
        Assert.Equal("Charlie", person.Name);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsExistingPerson()
    {
        using var ctx = TestDbContextFactory.Create();
        ctx.Persons.Add(new Data.Entities.Person { Name = "Dana" });
        await ctx.SaveChangesAsync();

        var svc = new PersonService(ctx);
        var person1 = await svc.GetOrCreateAsync("Dana");
        var person2 = await svc.GetOrCreateAsync("Dana");

        Assert.Equal(person1.Id, person2.Id);
        Assert.Equal(1, ctx.Persons.Count(p => p.Name == "Dana"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Examageddon.Tests/ --filter "PersonServiceTests" -v minimal
```
Expected: FAIL — PersonService not found.

- [ ] **Step 3: Implement PersonService**

`src/Examageddon.Services/PersonService.cs`:
```csharp
using Examageddon.Data;
using Examageddon.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Services;

public class PersonService(ExamageddonDbContext db)
{
    public async Task<List<Person>> GetAllAsync() =>
        await db.Persons.OrderBy(p => p.Name).ToListAsync();

    public async Task<Person> GetOrCreateAsync(string name)
    {
        var existing = await db.Persons.FirstOrDefaultAsync(p => p.Name == name);
        if (existing is not null) return existing;

        var person = new Person { Name = name };
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        return person;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Examageddon.Tests/ --filter "PersonServiceTests" -v minimal
```
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Services/PersonService.cs tests/Examageddon.Tests/PersonServiceTests.cs
git commit -m "feat: add PersonService with tests"
```

---

## Task 6: ExamManagementService (TDD)

**Files:**
- Create: `src/Examageddon.Services/ExamManagementService.cs`
- Create: `tests/Examageddon.Tests/ExamManagementServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/Examageddon.Tests/ExamManagementServiceTests.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Examageddon.Tests.Helpers;
using Xunit;

namespace Examageddon.Tests;

public class ExamManagementServiceTests
{
    private static Exam MakeExam(string title = "Test Exam") => new()
    {
        Title = title, PassingScorePercent = 70, ExamQuestionCount = 10
    };

    [Fact]
    public async Task CreateExamAsync_PersistsExam()
    {
        using var ctx = TestDbContextFactory.Create();
        var svc = new ExamManagementService(ctx);
        var exam = MakeExam();

        var created = await svc.CreateExamAsync(exam);

        Assert.NotEqual(0, created.Id);
        Assert.Equal("Test Exam", created.Title);
    }

    [Fact]
    public async Task GetAllExamsAsync_ReturnsAll()
    {
        using var ctx = TestDbContextFactory.Create();
        ctx.Exams.AddRange(MakeExam("A"), MakeExam("B"));
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(ctx);

        var exams = await svc.GetAllExamsAsync();

        Assert.Equal(2, exams.Count);
    }

    [Fact]
    public async Task AddQuestionAsync_AttachesToExam()
    {
        using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(ctx);

        var question = new Question
        {
            ExamId = exam.Id, Text = "Q1",
            AnswerOptions =
            [
                new AnswerOption { Text = "A", IsCorrect = true, OrderIndex = 0 },
                new AnswerOption { Text = "B", IsCorrect = false, OrderIndex = 1 }
            ]
        };
        var created = await svc.AddQuestionAsync(question);

        Assert.NotEqual(0, created.Id);
        Assert.Equal(2, ctx.AnswerOptions.Count(a => a.QuestionId == created.Id));
    }

    [Fact]
    public async Task DeleteQuestionAsync_RemovesQuestion()
    {
        using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        var question = new Question { Text = "Q1", ExamId = 0 };
        exam.Questions.Add(question);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(ctx);

        await svc.DeleteQuestionAsync(question.Id);

        Assert.Empty(ctx.Questions.Where(q => q.Id == question.Id));
    }

    [Fact]
    public async Task ImageRoundTrip_StoresAndRetrievesBlob()
    {
        using var ctx = TestDbContextFactory.Create();
        var exam = MakeExam();
        var question = new Question { Text = "Q1" };
        exam.Questions.Add(question);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        var svc = new ExamManagementService(ctx);

        byte[] imageData = [1, 2, 3, 4];
        await svc.SaveImageAsync(question.Id, imageData, "image/png");
        var (data, contentType) = await svc.GetImageAsync(question.Id);

        Assert.Equal(imageData, data);
        Assert.Equal("image/png", contentType);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Examageddon.Tests/ --filter "ExamManagementServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement ExamManagementService**

`src/Examageddon.Services/ExamManagementService.cs`:
```csharp
using Examageddon.Data;
using Examageddon.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Services;

public class ExamManagementService(ExamageddonDbContext db)
{
    public async Task<List<Exam>> GetAllExamsAsync() =>
        await db.Exams.Include(e => e.Questions).OrderBy(e => e.Title).ToListAsync();

    public async Task<Exam?> GetExamAsync(int id) =>
        await db.Exams.Include(e => e.Questions).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Exam> CreateExamAsync(Exam exam)
    {
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return exam;
    }

    public async Task UpdateExamAsync(Exam exam)
    {
        db.Exams.Update(exam);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExamAsync(int id)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is not null) { db.Exams.Remove(exam); await db.SaveChangesAsync(); }
    }

    public async Task<List<Question>> GetQuestionsAsync(int examId) =>
        await db.Questions.Include(q => q.AnswerOptions)
            .Where(q => q.ExamId == examId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync();

    public async Task<Question?> GetQuestionAsync(int questionId) =>
        await db.Questions.Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == questionId);

    public async Task<Question> AddQuestionAsync(Question question)
    {
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return question;
    }

    public async Task UpdateQuestionAsync(Question question)
    {
        db.Questions.Update(question);
        await db.SaveChangesAsync();
    }

    public async Task DeleteQuestionAsync(int questionId)
    {
        var q = await db.Questions.FindAsync(questionId);
        if (q is not null) { db.Questions.Remove(q); await db.SaveChangesAsync(); }
    }

    public async Task SaveImageAsync(int questionId, byte[] data, string contentType)
    {
        var q = await db.Questions.FindAsync(questionId);
        if (q is null) return;
        q.ImageData = data;
        q.ImageContentType = contentType;
        await db.SaveChangesAsync();
    }

    public async Task<(byte[]? data, string? contentType)> GetImageAsync(int questionId)
    {
        var q = await db.Questions.FindAsync(questionId);
        return (q?.ImageData, q?.ImageContentType);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Examageddon.Tests/ --filter "ExamManagementServiceTests" -v minimal
```
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Services/ExamManagementService.cs tests/Examageddon.Tests/ExamManagementServiceTests.cs
git commit -m "feat: add ExamManagementService with tests"
```

---

## Task 7: ExamSessionService (TDD)

**Files:**
- Create: `src/Examageddon.Services/ExamSessionService.cs`
- Create: `tests/Examageddon.Tests/ExamSessionServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/Examageddon.Tests/ExamSessionServiceTests.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Examageddon.Services.Models;
using Examageddon.Tests.Helpers;
using Xunit;

namespace Examageddon.Tests;

public class ExamSessionServiceTests
{
    private static async Task<(ExamageddonDbContext ctx, Person person, Exam exam)> SeedAsync(int questionCount = 5)
    {
        var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Tester" };
        var exam = new Exam { Title = "Demo", PassingScorePercent = 60, ExamQuestionCount = 3 };
        for (int i = 0; i < questionCount; i++)
        {
            var q = new Question { Text = $"Q{i}", OrderIndex = i };
            q.AnswerOptions.Add(new AnswerOption { Text = "Correct", IsCorrect = true, OrderIndex = 0 });
            q.AnswerOptions.Add(new AnswerOption { Text = "Wrong", IsCorrect = false, OrderIndex = 1 });
            exam.Questions.Add(q);
        }
        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();
        return (ctx, person, exam);
    }

    [Fact]
    public async Task CreateSession_All_CreatesAllQuestions()
    {
        var (ctx, person, exam) = await SeedAsync(5);
        var svc = new ExamSessionService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };

        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(5, session.TotalQuestions);
        Assert.Equal(5, ctx.SessionQuestions.Count(sq => sq.SessionId == session.Id));
    }

    [Fact]
    public async Task CreateSession_Limited_CreatesExamQuestionCount()
    {
        var (ctx, person, exam) = await SeedAsync(5);
        var svc = new ExamSessionService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.Limited, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };

        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        Assert.Equal(3, session.TotalQuestions); // ExamQuestionCount = 3
        Assert.Equal(3, ctx.SessionQuestions.Count(sq => sq.SessionId == session.Id));
    }

    [Fact]
    public async Task CreateSession_Random_ShufflesPositions()
    {
        var (ctx, person, exam) = await SeedAsync(10);
        var svc = new ExamSessionService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Random, FeedbackMode = FeedbackMode.Direct };

        var s1 = await svc.CreateSessionAsync(person.Id, exam.Id, opts);
        var s2 = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var order1 = ctx.SessionQuestions.Where(sq => sq.SessionId == s1.Id).OrderBy(sq => sq.Position).Select(sq => sq.QuestionId).ToList();
        var order2 = ctx.SessionQuestions.Where(sq => sq.SessionId == s2.Id).OrderBy(sq => sq.Position).Select(sq => sq.QuestionId).ToList();
        // With 10 questions, the chance both are identical is 1/10! — effectively impossible
        Assert.False(order1.SequenceEqual(order2));
    }

    [Fact]
    public async Task SubmitAnswer_RecordsCorrectAnswer()
    {
        var (ctx, person, exam) = await SeedAsync(2);
        var svc = new ExamSessionService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        var sq = ctx.SessionQuestions.OrderBy(sq => sq.Position).First(sq => sq.SessionId == session.Id);
        var correctOption = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);

        await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, correctOption.Id);

        var answer = ctx.SessionAnswers.First(sa => sa.SessionId == session.Id && sa.QuestionId == sq.QuestionId);
        Assert.True(answer.IsCorrect);
    }

    [Fact]
    public async Task CompleteSession_CalculatesPassFail()
    {
        var (ctx, person, exam) = await SeedAsync(3);
        var svc = new ExamSessionService(ctx);
        var opts = new SessionOptions { QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.AtEnd };
        var session = await svc.CreateSessionAsync(person.Id, exam.Id, opts);

        // Answer all correctly
        foreach (var sq in ctx.SessionQuestions.Where(sq => sq.SessionId == session.Id))
        {
            var correct = ctx.AnswerOptions.First(a => a.QuestionId == sq.QuestionId && a.IsCorrect);
            await svc.SubmitAnswerAsync(session.Id, sq.QuestionId, correct.Id);
        }

        var result = await svc.CompleteSessionAsync(session.Id);

        Assert.True(result.IsPassed); // 100% > 60% passing score
        Assert.Equal(3, result.CorrectAnswers);
        Assert.NotNull(ctx.ExamSessions.Find(session.Id)!.CompletedAt);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Examageddon.Tests/ --filter "ExamSessionServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement ExamSessionService**

`src/Examageddon.Services/ExamSessionService.cs`:
```csharp
using Examageddon.Data;
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Services;

public class ExamSessionService(ExamageddonDbContext db)
{
    public async Task<ExamSession> CreateSessionAsync(int personId, int examId, SessionOptions opts)
    {
        var exam = await db.Exams.Include(e => e.Questions).FirstAsync(e => e.Id == examId);

        var questions = exam.Questions.OrderBy(q => q.OrderIndex).ToList();
        if (opts.QuestionMode == QuestionMode.Limited)
            questions = questions.Take(exam.ExamQuestionCount).ToList();

        if (opts.OrderMode == OrderMode.Random)
            questions = [.. questions.OrderBy(_ => Guid.NewGuid())];

        var session = new ExamSession
        {
            PersonId = personId, ExamId = examId,
            StartedAt = DateTime.UtcNow,
            QuestionMode = opts.QuestionMode,
            OrderMode = opts.OrderMode,
            FeedbackMode = opts.FeedbackMode,
            TotalQuestions = questions.Count
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();

        for (int i = 0; i < questions.Count; i++)
            db.SessionQuestions.Add(new SessionQuestion { SessionId = session.Id, QuestionId = questions[i].Id, Position = i + 1 });

        await db.SaveChangesAsync();
        return session;
    }

    public async Task<SessionQuestionModel?> GetSessionQuestionAsync(int sessionId, int position, bool fromReview = false)
    {
        var session = await db.ExamSessions.FindAsync(sessionId);
        if (session is null) return null;

        var sq = await db.SessionQuestions
            .Include(s => s.Question).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.Position == position);
        if (sq is null) return null;

        var answer = await db.SessionAnswers
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.QuestionId == sq.QuestionId);

        var total = await db.SessionQuestions.CountAsync(s => s.SessionId == sessionId);

        return new SessionQuestionModel
        {
            SessionId = sessionId,
            Position = position,
            TotalQuestions = total,
            FeedbackMode = session.FeedbackMode,
            Question = sq.Question,
            AnswerOptions = sq.Question.AnswerOptions.OrderBy(a => a.OrderIndex).ToList(),
            ExistingAnswerOptionId = answer?.SelectedAnswerOptionId,
            IsFromReview = fromReview
        };
    }

    public async Task SubmitAnswerAsync(int sessionId, int questionId, int answerOptionId)
    {
        var option = await db.AnswerOptions.FindAsync(answerOptionId);
        if (option is null) return;

        var existing = await db.SessionAnswers
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.QuestionId == questionId);

        if (existing is not null)
        {
            existing.SelectedAnswerOptionId = answerOptionId;
            existing.IsCorrect = option.IsCorrect;
            existing.AnsweredAt = DateTime.UtcNow;
        }
        else
        {
            db.SessionAnswers.Add(new SessionAnswer
            {
                SessionId = sessionId, QuestionId = questionId,
                SelectedAnswerOptionId = answerOptionId,
                IsCorrect = option.IsCorrect,
                AnsweredAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task<SessionResultModel> CompleteSessionAsync(int sessionId)
    {
        var session = await db.ExamSessions
            .Include(s => s.Exam).Include(s => s.SessionAnswers)
            .FirstAsync(s => s.Id == sessionId);

        var correct = session.SessionAnswers.Count(a => a.IsCorrect);
        var scorePercent = session.TotalQuestions == 0 ? 0 : (double)correct / session.TotalQuestions * 100;

        session.CorrectAnswers = correct;
        session.IsPassed = scorePercent >= session.Exam.PassingScorePercent;
        session.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetResultAsync(sessionId);
    }

    public async Task<SessionResultModel> GetResultAsync(int sessionId)
    {
        var session = await db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionQuestions).ThenInclude(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Include(s => s.SessionAnswers).ThenInclude(sa => sa.SelectedAnswerOption)
            .FirstAsync(s => s.Id == sessionId);

        var items = session.SessionQuestions.OrderBy(sq => sq.Position).Select(sq =>
        {
            var answer = session.SessionAnswers.FirstOrDefault(sa => sa.QuestionId == sq.QuestionId);
            return new QuestionResultItem
            {
                Question = sq.Question,
                SelectedOption = answer?.SelectedAnswerOption,
                IsCorrect = answer?.IsCorrect ?? false,
                IsAnswered = answer is not null
            };
        }).ToList();

        return new SessionResultModel
        {
            SessionId = sessionId,
            ExamTitle = session.Exam.Title,
            TotalQuestions = session.TotalQuestions,
            CorrectAnswers = session.CorrectAnswers,
            PassingScorePercent = session.Exam.PassingScorePercent,
            IsPassed = session.IsPassed ?? false,
            QuestionResults = items
        };
    }

    public async Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId) =>
        await db.SessionQuestions
            .Include(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Where(sq => sq.SessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToListAsync();
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Examageddon.Tests/ --filter "ExamSessionServiceTests" -v minimal
```
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Services/ExamSessionService.cs tests/Examageddon.Tests/ExamSessionServiceTests.cs
git commit -m "feat: add ExamSessionService with tests"
```

---

## Task 8: HistoryService (TDD)

**Files:**
- Create: `src/Examageddon.Services/HistoryService.cs`
- Create: `tests/Examageddon.Tests/HistoryServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/Examageddon.Tests/HistoryServiceTests.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Examageddon.Tests.Helpers;
using Xunit;

namespace Examageddon.Tests;

public class HistoryServiceTests
{
    [Fact]
    public async Task GetHistoryForPersonAsync_ReturnsOnlyCompletedSessions()
    {
        using var ctx = TestDbContextFactory.Create();
        var person = new Person { Name = "Test" };
        var exam = new Exam { Title = "E1", PassingScorePercent = 60, ExamQuestionCount = 1 };
        ctx.Persons.Add(person);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();

        ctx.ExamSessions.AddRange(
            new ExamSession { PersonId = person.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 1, IsPassed = true, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct },
            new ExamSession { PersonId = person.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = null, TotalQuestions = 1, CorrectAnswers = 0, IsPassed = null, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct }
        );
        await ctx.SaveChangesAsync();

        var svc = new HistoryService(ctx);
        var history = await svc.GetHistoryForPersonAsync(person.Id);

        Assert.Single(history);
        Assert.True(history[0].IsPassed);
    }

    [Fact]
    public async Task GetHistoryForPersonAsync_ReturnsOnlyOwnSessions()
    {
        using var ctx = TestDbContextFactory.Create();
        var p1 = new Person { Name = "Alice" };
        var p2 = new Person { Name = "Bob" };
        var exam = new Exam { Title = "E1", PassingScorePercent = 60, ExamQuestionCount = 1 };
        ctx.Persons.AddRange(p1, p2);
        ctx.Exams.Add(exam);
        await ctx.SaveChangesAsync();

        ctx.ExamSessions.AddRange(
            new ExamSession { PersonId = p1.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 1, IsPassed = true, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct },
            new ExamSession { PersonId = p2.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, TotalQuestions = 1, CorrectAnswers = 0, IsPassed = false, QuestionMode = QuestionMode.All, OrderMode = OrderMode.Default, FeedbackMode = FeedbackMode.Direct }
        );
        await ctx.SaveChangesAsync();

        var svc = new HistoryService(ctx);
        var history = await svc.GetHistoryForPersonAsync(p1.Id);

        Assert.Single(history);
        Assert.Equal("Alice", ctx.Persons.Find(history[0].SessionId == 0 ? 0 : p1.Id)!.Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Examageddon.Tests/ --filter "HistoryServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement HistoryService**

`src/Examageddon.Services/HistoryService.cs`:
```csharp
using Examageddon.Data;
using Examageddon.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Services;

public class HistoryService(ExamageddonDbContext db)
{
    public async Task<List<HistoryEntryModel>> GetHistoryForPersonAsync(int personId) =>
        await db.ExamSessions
            .Include(s => s.Exam)
            .Where(s => s.PersonId == personId && s.CompletedAt != null)
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => new HistoryEntryModel
            {
                SessionId = s.Id,
                ExamTitle = s.Exam.Title,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt!.Value,
                TotalQuestions = s.TotalQuestions,
                CorrectAnswers = s.CorrectAnswers,
                IsPassed = s.IsPassed ?? false
            })
            .ToListAsync();
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Examageddon.Tests/ --filter "HistoryServiceTests" -v minimal
```
Expected: 2 passed.

- [ ] **Step 5: Build all**

```bash
dotnet build Examageddon.sln
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Services/HistoryService.cs tests/Examageddon.Tests/HistoryServiceTests.cs
git commit -m "feat: add HistoryService with tests"
```

---

## Task 9: Web Project Setup

**Files:**
- Modify: `src/Examageddon.Web/Program.cs`
- Create: `src/Examageddon.Web/Pages/_ViewImports.cshtml`
- Create: `src/Examageddon.Web/Pages/_ViewStart.cshtml`
- Create: `src/Examageddon.Web/Pages/Shared/_Layout.cshtml`

- [ ] **Step 1: Configure Program.cs**

Replace contents of `src/Examageddon.Web/Program.cs`:
```csharp
using Examageddon.Data;
using Examageddon.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSession(opts => opts.IdleTimeout = TimeSpan.FromDays(7));

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "examageddon.db");
builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<PersonService>();
builder.Services.AddScoped<ExamManagementService>();
builder.Services.AddScoped<ExamSessionService>();
builder.Services.AddScoped<HistoryService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExamageddonDbContext>();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseSession();
app.UseRouting();

// Image endpoint
app.MapGet("/images/question/{id:int}", async (int id, ExamManagementService svc) =>
{
    var (data, contentType) = await svc.GetImageAsync(id);
    if (data is null) return Results.NotFound();
    return Results.File(data, contentType ?? "application/octet-stream");
});

app.MapRazorPages();
app.Run();
```

- [ ] **Step 2: Create _ViewImports.cshtml**

`src/Examageddon.Web/Pages/_ViewImports.cshtml`:
```razor
@using Examageddon.Web.Pages
@namespace Examageddon.Web.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 3: Create _ViewStart.cshtml**

`src/Examageddon.Web/Pages/_ViewStart.cshtml`:
```razor
@{
    Layout = "_Layout";
}
```

- [ ] **Step 4: Create _Layout.cshtml**

`src/Examageddon.Web/Pages/Shared/_Layout.cshtml`:
```razor
<!DOCTYPE html>
<html lang="en" class="h-full">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] — Examageddon</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script src="https://unpkg.com/htmx.org@2.0.0/dist/htmx.min.js"></script>
    <script>
        tailwind.config = {
            theme: {
                extend: {
                    colors: {
                        arcade: { dark: '#0f172a', card: '#1e293b', border: '#334155', muted: '#94a3b8', text: '#f1f5f9' },
                        fire: { DEFAULT: '#f59e0b', hot: '#ef4444' }
                    }
                }
            }
        }
    </script>
</head>
<body class="h-full bg-arcade-dark text-arcade-text font-sans">

    <nav class="bg-arcade-card border-b border-arcade-border px-6 py-3 flex items-center justify-between">
        <a href="/exams" class="text-xl font-black tracking-widest uppercase bg-gradient-to-r from-fire to-fire-hot bg-clip-text text-transparent">
            ⚡ Examageddon
        </a>
        <div class="flex items-center gap-6 text-sm">
            @if (Context.Session.GetString("PersonName") is { } name)
            {
                <span class="text-arcade-muted">👤 <span class="text-arcade-text font-semibold">@name</span></span>
            }
            <a href="/history" class="text-arcade-muted hover:text-arcade-text transition">History</a>
            <a href="/manage" class="text-arcade-muted hover:text-arcade-text transition">Manage</a>
        </div>
    </nav>

    <main class="max-w-4xl mx-auto px-4 py-8">
        @RenderBody()
    </main>

    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 5: Build and run briefly to verify startup**

```bash
dotnet build src/Examageddon.Web/Examageddon.Web.csproj
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/
git commit -m "feat: configure web project with layout and CDN scripts"
```

---

## Task 10: Name Picker Page (/)

**Files:**
- Modify: `src/Examageddon.Web/Pages/Index.cshtml`
- Modify: `src/Examageddon.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Create page model**

`src/Examageddon.Web/Pages/Index.cshtml.cs`:
```csharp
using Examageddon.Services;
using Examageddon.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages;

public class IndexModel(PersonService personService) : PageModel
{
    public List<Person> People { get; set; } = [];
    [BindProperty] public string NewName { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        People = await personService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostSelectAsync(string name)
    {
        var person = await personService.GetOrCreateAsync(name);
        HttpContext.Session.SetInt32("PersonId", person.Id);
        HttpContext.Session.SetString("PersonName", person.Name);
        return RedirectToPage("/Exams/Index");
    }

    public async Task<IActionResult> OnPostNewAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return RedirectToPage();
        var person = await personService.GetOrCreateAsync(NewName.Trim());
        HttpContext.Session.SetInt32("PersonId", person.Id);
        HttpContext.Session.SetString("PersonName", person.Name);
        return RedirectToPage("/Exams/Index");
    }
}
```

- [ ] **Step 2: Create page view**

`src/Examageddon.Web/Pages/Index.cshtml`:
```razor
@page
@model IndexModel
@{ ViewData["Title"] = "Who are you?"; }

<div class="text-center mb-10">
    <h1 class="text-4xl font-black tracking-tight mb-2">⚡ Welcome to <span class="bg-gradient-to-r from-fire to-fire-hot bg-clip-text text-transparent">Examageddon</span></h1>
    <p class="text-arcade-muted">Select your name to get started</p>
</div>

@if (Model.People.Any())
{
    <div class="grid grid-cols-2 sm:grid-cols-3 gap-3 mb-8">
        @foreach (var person in Model.People)
        {
            <form method="post" asp-page-handler="Select">
                <input type="hidden" name="name" value="@person.Name" />
                <button type="submit"
                    class="w-full bg-arcade-card border border-arcade-border rounded-xl p-4 text-left hover:border-fire hover:bg-slate-800 transition font-semibold">
                    👤 @person.Name
                </button>
            </form>
        }
    </div>
}

<div class="bg-arcade-card border border-arcade-border rounded-xl p-6">
    <h2 class="text-sm font-bold uppercase tracking-widest text-arcade-muted mb-4">New name</h2>
    <form method="post" asp-page-handler="New" class="flex gap-3">
        <input asp-for="NewName" placeholder="Enter your name..."
            class="flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
        <button type="submit"
            class="bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold px-6 py-2 rounded-lg hover:opacity-90 transition">
            Start →
        </button>
    </form>
</div>
```

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Index.cshtml src/Examageddon.Web/Pages/Index.cshtml.cs
git commit -m "feat: add name picker page"
```

---

## Task 11: Exam Selector Page

**Files:**
- Create: `src/Examageddon.Web/Pages/Exams/Index.cshtml`
- Create: `src/Examageddon.Web/Pages/Exams/Index.cshtml.cs`

- [ ] **Step 1: Create page model**

`src/Examageddon.Web/Pages/Exams/Index.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Exams;

public class IndexModel(ExamManagementService examService) : PageModel
{
    public List<Exam> Exams { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (HttpContext.Session.GetInt32("PersonId") is null)
            return RedirectToPage("/Index");

        Exams = await examService.GetAllExamsAsync();
        return Page();
    }
}
```

- [ ] **Step 2: Create page view**

`src/Examageddon.Web/Pages/Exams/Index.cshtml`:
```razor
@page
@model Examageddon.Web.Pages.Exams.IndexModel
@{ ViewData["Title"] = "Select Exam"; }

<h1 class="text-3xl font-black mb-2">Select an Exam</h1>
<p class="text-arcade-muted mb-8">Choose your challenge</p>

@if (!Model.Exams.Any())
{
    <div class="bg-arcade-card border border-arcade-border rounded-xl p-8 text-center text-arcade-muted">
        No exams yet. <a href="/manage" class="text-fire hover:underline">Create one in Manage.</a>
    </div>
}
else
{
    <div class="grid gap-4">
        @foreach (var exam in Model.Exams)
        {
            <a href="/exams/@exam.Id/setup"
               class="bg-arcade-card border border-arcade-border rounded-xl p-6 hover:border-fire transition group flex items-center justify-between">
                <div>
                    <h2 class="text-xl font-bold group-hover:text-fire transition">@exam.Title</h2>
                    @if (!string.IsNullOrEmpty(exam.Description))
                    {
                        <p class="text-arcade-muted text-sm mt-1">@exam.Description</p>
                    }
                    <div class="flex gap-4 mt-3 text-sm text-arcade-muted">
                        <span>📋 @exam.Questions.Count questions</span>
                        <span>🎯 Pass at @exam.PassingScorePercent%</span>
                        <span>📝 Exam: @exam.ExamQuestionCount q's</span>
                    </div>
                </div>
                <span class="text-fire text-2xl opacity-0 group-hover:opacity-100 transition">→</span>
            </a>
        }
    </div>
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Exams/
git commit -m "feat: add exam selector page"
```

---

## Task 12: Session Setup Page

**Files:**
- Create: `src/Examageddon.Web/Pages/Exams/Setup.cshtml`
- Create: `src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs`

- [ ] **Step 1: Create page model**

`src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Exams;

public class SetupModel(ExamManagementService examService, ExamSessionService sessionService) : PageModel
{
    public Exam Exam { get; set; } = null!;

    [BindProperty] public QuestionMode QuestionMode { get; set; } = QuestionMode.All;
    [BindProperty] public OrderMode OrderMode { get; set; } = OrderMode.Default;
    [BindProperty] public FeedbackMode FeedbackMode { get; set; } = FeedbackMode.Direct;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (HttpContext.Session.GetInt32("PersonId") is null)
            return RedirectToPage("/Index");

        var exam = await examService.GetExamAsync(id);
        if (exam is null) return NotFound();
        Exam = exam;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var personId = HttpContext.Session.GetInt32("PersonId");
        if (personId is null) return RedirectToPage("/Index");

        var opts = new SessionOptions { QuestionMode = QuestionMode, OrderMode = OrderMode, FeedbackMode = FeedbackMode };
        var session = await sessionService.CreateSessionAsync(personId.Value, id, opts);
        return RedirectToPage("/Sessions/Question", new { sessionId = session.Id, n = 1 });
    }
}
```

- [ ] **Step 2: Create page view**

`src/Examageddon.Web/Pages/Exams/Setup.cshtml`:
```razor
@page "/exams/{id:int}/setup"
@model Examageddon.Web.Pages.Exams.SetupModel
@using Examageddon.Data.Enums
@{ ViewData["Title"] = "Setup"; }

<h1 class="text-3xl font-black mb-1">@Model.Exam.Title</h1>
<p class="text-arcade-muted mb-8">Configure your session</p>

<form method="post" class="space-y-6">

    <div class="bg-arcade-card border border-arcade-border rounded-xl p-6">
        <h2 class="text-sm font-bold uppercase tracking-widest text-arcade-muted mb-4">Questions</h2>
        <div class="grid grid-cols-2 gap-3">
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="QuestionMode" value="All" class="accent-yellow-500" />
                <span>All <span class="text-arcade-muted text-sm">(@Model.Exam.Questions.Count questions)</span></span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="QuestionMode" value="Limited" class="accent-yellow-500" />
                <span>Exam mode <span class="text-arcade-muted text-sm">(@Model.Exam.ExamQuestionCount questions)</span></span>
            </label>
        </div>
    </div>

    <div class="bg-arcade-card border border-arcade-border rounded-xl p-6">
        <h2 class="text-sm font-bold uppercase tracking-widest text-arcade-muted mb-4">Order</h2>
        <div class="grid grid-cols-2 gap-3">
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="OrderMode" value="Default" class="accent-yellow-500" />
                <span>Default order</span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="OrderMode" value="Random" class="accent-yellow-500" />
                <span>🔀 Randomized</span>
            </label>
        </div>
    </div>

    <div class="bg-arcade-card border border-arcade-border rounded-xl p-6">
        <h2 class="text-sm font-bold uppercase tracking-widest text-arcade-muted mb-4">Feedback</h2>
        <div class="grid grid-cols-2 gap-3">
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="FeedbackMode" value="Direct" class="accent-yellow-500" />
                <span>Direct <span class="text-arcade-muted text-sm">(after each question)</span></span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer">
                <input type="radio" asp-for="FeedbackMode" value="AtEnd" class="accent-yellow-500" />
                <span>At the end <span class="text-arcade-muted text-sm">(review first)</span></span>
            </label>
        </div>
    </div>

    <button type="submit"
        class="w-full bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-black text-lg py-4 rounded-xl hover:opacity-90 transition">
        ⚡ Start Exam
    </button>
</form>
```

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/Exams/Setup.cshtml src/Examageddon.Web/Pages/Exams/Setup.cshtml.cs
git commit -m "feat: add session setup page"
```

---

## Task 13: Question Page with HTMX

**Files:**
- Create: `src/Examageddon.Web/Pages/Sessions/Question.cshtml`
- Create: `src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs`
- Create: `src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`

- [ ] **Step 1: Create page model**

`src/Examageddon.Web/Pages/Sessions/Question.cshtml.cs`:
```csharp
using Examageddon.Data.Enums;
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class QuestionModel(ExamSessionService sessionService) : PageModel
{
    public SessionQuestionModel? SessionQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int sessionId, int n, bool fromReview = false)
    {
        if (HttpContext.Session.GetInt32("PersonId") is null)
            return RedirectToPage("/Index");

        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n, fromReview);
        if (SessionQuestion is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAnswerAsync(int sessionId, int n, int questionId, int answerId)
    {
        await sessionService.SubmitAnswerAsync(sessionId, questionId, answerId);
        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n);
        if (SessionQuestion is null) return NotFound();

        return Partial("_AnswerFeedback", SessionQuestion);
    }
}
```

- [ ] **Step 2: Create question page view**

`src/Examageddon.Web/Pages/Sessions/Question.cshtml`:
```razor
@page "/sessions/{sessionId:int}/question/{n:int}"
@model Examageddon.Web.Pages.Sessions.QuestionModel
@using Examageddon.Data.Enums
@{
    ViewData["Title"] = $"Question {Model.SessionQuestion!.Position}";
    var sq = Model.SessionQuestion!;
}

<div class="mb-6">
    <div class="flex justify-between items-center mb-2 text-sm text-arcade-muted">
        <span>Question @sq.Position / @sq.TotalQuestions</span>
        @if (sq.FeedbackMode == FeedbackMode.AtEnd && sq.IsFromReview)
        {
            <a href="/sessions/@sq.SessionId/review" class="text-fire hover:underline">← Back to Review</a>
        }
    </div>
    <div class="h-2 bg-arcade-card rounded-full overflow-hidden">
        <div class="h-full bg-gradient-to-r from-fire to-fire-hot rounded-full transition-all"
             style="width: @(sq.Position * 100 / sq.TotalQuestions)%"></div>
    </div>
</div>

<div class="bg-arcade-card border border-arcade-border rounded-xl p-6 mb-6">
    <p class="text-xl font-semibold mb-4">@sq.Question.Text</p>
    @if (sq.Question.ImageData != null)
    {
        <img src="/images/question/@sq.Question.Id" alt="Question image"
             class="rounded-lg max-h-64 object-contain mb-4 border border-arcade-border" />
    }
</div>

<div id="answer-container">
    @await Html.PartialAsync("_AnswerFeedback", sq)
</div>

@if (sq.FeedbackMode == FeedbackMode.AtEnd && !sq.IsFromReview)
{
    <div class="flex justify-between mt-6">
        @if (sq.Position > 1)
        {
            <a href="/sessions/@sq.SessionId/question/@(sq.Position - 1)"
               class="px-6 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
                ← Back
            </a>
        }
        else { <div></div> }

        @if (sq.Position < sq.TotalQuestions)
        {
            <a href="/sessions/@sq.SessionId/question/@(sq.Position + 1)"
               class="px-6 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
                Next →
            </a>
        }
        else
        {
            <a href="/sessions/@sq.SessionId/review"
               class="px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
                Review →
            </a>
        }
    </div>
}
```

- [ ] **Step 3: Create answer feedback partial**

`src/Examageddon.Web/Pages/Sessions/_AnswerFeedback.cshtml`:
```razor
@model Examageddon.Services.Models.SessionQuestionModel
@using Examageddon.Data.Enums

<div class="space-y-3">
    @foreach (var option in Model.AnswerOptions)
    {
        var isSelected = Model.ExistingAnswerOptionId == option.Id;
        var isAnswered = Model.IsAnswered && Model.FeedbackMode == FeedbackMode.Direct;

        string borderClass, bgClass, textClass;

        if (isAnswered)
        {
            if (option.IsCorrect)
            { borderClass = "border-green-500"; bgClass = "bg-green-500/10"; textClass = "text-green-400"; }
            else if (isSelected)
            { borderClass = "border-red-500"; bgClass = "bg-red-500/10"; textClass = "text-red-400"; }
            else
            { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-muted opacity-50"; }
        }
        else if (isSelected)
        { borderClass = "border-fire"; bgClass = "bg-fire/10"; textClass = "text-fire"; }
        else
        { borderClass = "border-arcade-border"; bgClass = ""; textClass = "text-arcade-text"; }

        @if (isAnswered)
        {
            <div class="border @borderClass @bgClass rounded-xl p-4 @textClass font-medium flex items-center gap-3">
                @if (option.IsCorrect) { <span>✓</span> }
                else if (isSelected) { <span>✗</span> }
                else { <span class="w-4"></span> }
                @option.Text
            </div>
        }
        else
        {
            <button type="button"
                hx-post="/sessions/@Model.SessionId/question/@Model.Position?handler=Answer&questionId=@Model.Question.Id&answerId=@option.Id&n=@Model.Position&sessionId=@Model.SessionId"
                hx-target="#answer-container"
                hx-swap="innerHTML"
                class="w-full text-left border @borderClass @bgClass rounded-xl p-4 @textClass font-medium hover:border-fire hover:text-fire transition">
                @option.Text
            </button>
        }
    }
</div>

@if (Model.IsAnswered && Model.FeedbackMode == FeedbackMode.Direct)
{
    <div class="mt-6">
        @if (Model.Position < Model.TotalQuestions)
        {
            <a href="/sessions/@Model.SessionId/question/@(Model.Position + 1)"
               class="inline-block w-full text-center px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
                Next →
            </a>
        }
        else
        {
            <form method="post" action="/sessions/@Model.SessionId/complete">
                <button type="submit"
                    class="w-full px-6 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
                    Finish 🏁
                </button>
            </form>
        }
    </div>
}
```

- [ ] **Step 4: Add Complete handler to Program.cs**

Add before `app.MapRazorPages()` in `Program.cs`:
```csharp
app.MapPost("/sessions/{id:int}/complete", async (int id, ExamSessionService svc) =>
{
    await svc.CompleteSessionAsync(id);
    return Results.Redirect($"/sessions/{id}/result");
});
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Examageddon.Web/Examageddon.Web.csproj
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/
git commit -m "feat: add question page with HTMX answer submission"
```

---

## Task 14: Review & Result Pages

**Files:**
- Create: `src/Examageddon.Web/Pages/Sessions/Review.cshtml`
- Create: `src/Examageddon.Web/Pages/Sessions/Review.cshtml.cs`
- Create: `src/Examageddon.Web/Pages/Sessions/Result.cshtml`
- Create: `src/Examageddon.Web/Pages/Sessions/Result.cshtml.cs`

- [ ] **Step 1: Create Review page model**

`src/Examageddon.Web/Pages/Sessions/Review.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class ReviewModel(ExamSessionService sessionService) : PageModel
{
    public List<SessionQuestion> SessionQuestions { get; set; } = [];
    public Dictionary<int, SessionAnswer> Answers { get; set; } = [];
    public int SessionId { get; set; }

    public async Task<IActionResult> OnGetAsync(int sessionId)
    {
        if (HttpContext.Session.GetInt32("PersonId") is null)
            return RedirectToPage("/Index");

        SessionId = sessionId;
        SessionQuestions = await sessionService.GetReviewQuestionsAsync(sessionId);

        var result = await sessionService.GetResultAsync(sessionId);
        // Build answer lookup
        foreach (var q in result.QuestionResults)
            if (q.IsAnswered)
                Answers[q.Question.Id] = new SessionAnswer
                {
                    SelectedAnswerOptionId = q.SelectedOption!.Id,
                    IsCorrect = q.IsCorrect
                };

        return Page();
    }
}
```

- [ ] **Step 2: Create Review page view**

`src/Examageddon.Web/Pages/Sessions/Review.cshtml`:
```razor
@page "/sessions/{sessionId:int}/review"
@model Examageddon.Web.Pages.Sessions.ReviewModel
@{ ViewData["Title"] = "Review Answers"; }

<h1 class="text-3xl font-black mb-2">Review Your Answers</h1>
<p class="text-arcade-muted mb-8">Click any question to change your answer.</p>

<div class="space-y-3 mb-8">
    @foreach (var sq in Model.SessionQuestions)
    {
        var answered = Model.Answers.TryGetValue(sq.QuestionId, out var ans);
        var selectedOption = sq.Question.AnswerOptions.FirstOrDefault(a => answered && a.Id == ans!.SelectedAnswerOptionId);
        <a href="/sessions/@Model.SessionId/question/@sq.Position?fromReview=true"
           class="flex items-center gap-4 bg-arcade-card border border-arcade-border rounded-xl p-4 hover:border-fire transition">
            <span class="text-arcade-muted text-sm w-6 text-center font-bold">@sq.Position</span>
            <div class="flex-1">
                <p class="font-medium text-sm">@sq.Question.Text</p>
                @if (selectedOption is not null)
                {
                    <p class="text-fire text-sm mt-1">→ @selectedOption.Text</p>
                }
                else
                {
                    <p class="text-red-400 text-sm mt-1">⚠ Not answered</p>
                }
            </div>
            <span class="text-arcade-muted text-sm">Edit →</span>
        </a>
    }
</div>

<form method="post" action="/sessions/@Model.SessionId/complete">
    <button type="submit"
        class="w-full py-4 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-black text-lg rounded-xl hover:opacity-90 transition">
        Submit Exam 🏁
    </button>
</form>
```

- [ ] **Step 3: Create Result page model**

`src/Examageddon.Web/Pages/Sessions/Result.cshtml.cs`:
```csharp
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class ResultModel(ExamSessionService sessionService) : PageModel
{
    public SessionResultModel Result { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int sessionId)
    {
        if (HttpContext.Session.GetInt32("PersonId") is null)
            return RedirectToPage("/Index");

        Result = await sessionService.GetResultAsync(sessionId);
        return Page();
    }
}
```

- [ ] **Step 4: Create Result page view**

`src/Examageddon.Web/Pages/Sessions/Result.cshtml`:
```razor
@page "/sessions/{sessionId:int}/result"
@model Examageddon.Web.Pages.Sessions.ResultModel
@{ ViewData["Title"] = "Result"; var r = Model.Result; }

<div class="text-center mb-10">
    @if (r.IsPassed)
    {
        <div class="text-6xl mb-4">🏆</div>
        <h1 class="text-4xl font-black text-green-400 mb-2">You Passed!</h1>
    }
    else
    {
        <div class="text-6xl mb-4">💀</div>
        <h1 class="text-4xl font-black text-red-400 mb-2">Not Quite...</h1>
    }
    <p class="text-arcade-muted">@r.ExamTitle</p>
</div>

<div class="bg-arcade-card border border-arcade-border rounded-xl p-6 mb-8">
    <div class="grid grid-cols-3 gap-4 text-center">
        <div>
            <div class="text-3xl font-black @(r.IsPassed ? "text-green-400" : "text-red-400")">@r.ScorePercent%</div>
            <div class="text-arcade-muted text-sm">Score</div>
        </div>
        <div>
            <div class="text-3xl font-black">@r.CorrectAnswers / @r.TotalQuestions</div>
            <div class="text-arcade-muted text-sm">Correct</div>
        </div>
        <div>
            <div class="text-3xl font-black text-fire">@r.PassingScorePercent%</div>
            <div class="text-arcade-muted text-sm">To Pass</div>
        </div>
    </div>
</div>

<div class="space-y-2 mb-8">
    @foreach (var item in r.QuestionResults)
    {
        <div class="flex items-start gap-3 bg-arcade-card border border-arcade-border rounded-xl p-4">
            <span class="text-lg">@(item.IsCorrect ? "✅" : "❌")</span>
            <div class="flex-1">
                <p class="text-sm font-medium">@item.Question.Text</p>
                @if (item.SelectedOption is not null)
                {
                    <p class="text-sm mt-1 @(item.IsCorrect ? "text-green-400" : "text-red-400")">
                        Your answer: @item.SelectedOption.Text
                    </p>
                }
                @if (!item.IsCorrect)
                {
                    var correct = item.Question.AnswerOptions.FirstOrDefault(a => a.IsCorrect);
                    if (correct is not null)
                    {
                        <p class="text-sm text-green-400 mt-1">Correct: @correct.Text</p>
                    }
                }
            </div>
        </div>
    }
</div>

<div class="flex gap-4">
    <a href="/exams" class="flex-1 text-center py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-fire transition">
        ← Back to Exams
    </a>
    <a href="/history" class="flex-1 text-center py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
        View History
    </a>
</div>
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Examageddon.Web/Examageddon.Web.csproj
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/Pages/Sessions/
git commit -m "feat: add review and result pages"
```

---

## Task 15: History Page

**Files:**
- Create: `src/Examageddon.Web/Pages/History/Index.cshtml`
- Create: `src/Examageddon.Web/Pages/History/Index.cshtml.cs`

- [ ] **Step 1: Create page model**

`src/Examageddon.Web/Pages/History/Index.cshtml.cs`:
```csharp
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.History;

public class IndexModel(HistoryService historyService) : PageModel
{
    public List<HistoryEntryModel> Entries { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32("PersonId");
        if (personId is null) return RedirectToPage("/Index");

        Entries = await historyService.GetHistoryForPersonAsync(personId.Value);
        return Page();
    }
}
```

- [ ] **Step 2: Create page view**

`src/Examageddon.Web/Pages/History/Index.cshtml`:
```razor
@page
@model Examageddon.Web.Pages.History.IndexModel
@{ ViewData["Title"] = "My History"; }

<h1 class="text-3xl font-black mb-2">My History</h1>
<p class="text-arcade-muted mb-8">Your past exam attempts</p>

@if (!Model.Entries.Any())
{
    <div class="bg-arcade-card border border-arcade-border rounded-xl p-8 text-center text-arcade-muted">
        No attempts yet. <a href="/exams" class="text-fire hover:underline">Take an exam!</a>
    </div>
}
else
{
    <div class="space-y-3">
        @foreach (var entry in Model.Entries)
        {
            <div class="bg-arcade-card border border-arcade-border rounded-xl p-5 flex items-center justify-between">
                <div>
                    <p class="font-bold">@entry.ExamTitle</p>
                    <p class="text-arcade-muted text-sm">@entry.CompletedAt.ToLocalTime().ToString("MMM d, yyyy HH:mm")</p>
                </div>
                <div class="text-right">
                    <div class="text-xl font-black @(entry.IsPassed ? "text-green-400" : "text-red-400")">
                        @entry.ScorePercent%
                    </div>
                    <div class="text-sm @(entry.IsPassed ? "text-green-400" : "text-red-400")">
                        @(entry.IsPassed ? "PASSED" : "FAILED")
                    </div>
                    <div class="text-arcade-muted text-xs">@entry.CorrectAnswers / @entry.TotalQuestions correct</div>
                </div>
            </div>
        }
    </div>
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Examageddon.Web/Pages/History/
git commit -m "feat: add history page"
```

---

## Task 16: Management — Exam CRUD

**Files:**
- Create: `src/Examageddon.Web/Pages/Manage/Index.cshtml` + `.cs`
- Create: `src/Examageddon.Web/Pages/Manage/ExamForm.cshtml` + `.cs`

- [ ] **Step 1: Create Manage Index**

`src/Examageddon.Web/Pages/Manage/Index.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class IndexModel(ExamManagementService examService) : PageModel
{
    public List<Exam> Exams { get; set; } = [];

    public async Task OnGetAsync() => Exams = await examService.GetAllExamsAsync();

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await examService.DeleteExamAsync(id);
        return RedirectToPage();
    }
}
```

`src/Examageddon.Web/Pages/Manage/Index.cshtml`:
```razor
@page
@model Examageddon.Web.Pages.Manage.IndexModel
@{ ViewData["Title"] = "Manage Exams"; }

<div class="flex items-center justify-between mb-8">
    <h1 class="text-3xl font-black">Manage Exams</h1>
    <a href="/manage/exams/create" class="px-5 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg hover:opacity-90 transition">
        + New Exam
    </a>
</div>

@if (!Model.Exams.Any())
{
    <div class="bg-arcade-card border border-arcade-border rounded-xl p-8 text-center text-arcade-muted">
        No exams yet. Create one!
    </div>
}
else
{
    <div class="space-y-3">
        @foreach (var exam in Model.Exams)
        {
            <div class="bg-arcade-card border border-arcade-border rounded-xl p-5 flex items-center justify-between">
                <div>
                    <p class="font-bold">@exam.Title</p>
                    <p class="text-arcade-muted text-sm">@exam.Questions.Count questions · Pass at @exam.PassingScorePercent%</p>
                </div>
                <div class="flex gap-3">
                    <a href="/manage/exams/@exam.Id/questions" class="px-4 py-2 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition">
                        Questions
                    </a>
                    <a href="/manage/exams/@exam.Id/edit" class="px-4 py-2 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition">
                        Edit
                    </a>
                    <form method="post" asp-page-handler="Delete" asp-route-id="@exam.Id"
                          onsubmit="return confirm('Delete @exam.Title?')">
                        <button type="submit" class="px-4 py-2 bg-arcade-dark border border-red-800 rounded-lg text-sm text-red-400 hover:border-red-500 transition">
                            Delete
                        </button>
                    </form>
                </div>
            </div>
        }
    </div>
}
```

- [ ] **Step 2: Create ExamForm page (shared for create/edit)**

The form page handles both create (`id=0`) and edit (`id={examId}`). The "New Exam" button links to `/manage/exams/0/edit`; the edit link uses `/manage/exams/{id}/edit`.

`src/Examageddon.Web/Pages/Manage/ExamForm.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ExamFormModel(ExamManagementService examService) : PageModel
{
    [BindProperty] public Exam Exam { get; set; } = new();
    public bool IsEdit => Exam.Id != 0;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id != 0)
        {
            var exam = await examService.GetExamAsync(id);
            if (exam is null) return NotFound();
            Exam = exam;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Exam.Id == 0)
            await examService.CreateExamAsync(Exam);
        else
            await examService.UpdateExamAsync(Exam);

        return RedirectToPage("/Manage/Index");
    }
}
```

`src/Examageddon.Web/Pages/Manage/ExamForm.cshtml`:
```razor
@page "/manage/exams/{id:int}/edit"
@model Examageddon.Web.Pages.Manage.ExamFormModel
@{ ViewData["Title"] = Model.IsEdit ? "Edit Exam" : "New Exam"; }

<h1 class="text-3xl font-black mb-8">@ViewData["Title"]</h1>

<form method="post" class="space-y-5 max-w-lg">
    <input type="hidden" asp-for="Exam.Id" />

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Title *</label>
        <input asp-for="Exam.Title" required
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
    </div>
    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Description</label>
        <textarea asp-for="Exam.Description" rows="3"
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire"></textarea>
    </div>
    <div class="grid grid-cols-2 gap-4">
        <div>
            <label class="block text-sm font-bold text-arcade-muted mb-2">Passing Score %</label>
            <input asp-for="Exam.PassingScorePercent" type="number" min="1" max="100" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
        </div>
        <div>
            <label class="block text-sm font-bold text-arcade-muted mb-2">Exam Question Count</label>
            <input asp-for="Exam.ExamQuestionCount" type="number" min="1" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire" />
        </div>
    </div>

    <div class="flex gap-4 pt-2">
        <button type="submit"
            class="px-8 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
            @(Model.IsEdit ? "Save Changes" : "Create Exam")
        </button>
        <a href="/manage" class="px-8 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
            Cancel
        </a>
    </div>
</form>
```

In `Manage/Index.cshtml`, the "New Exam" button links to `/manage/exams/0/edit`:
```razor
<a href="/manage/exams/0/edit" class="px-5 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg hover:opacity-90 transition">
    + New Exam
</a>
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Examageddon.Web/Examageddon.Web.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/
git commit -m "feat: add management exam CRUD pages"
```

---

## Task 17: Management — Question CRUD

**Files:**
- Create: `src/Examageddon.Web/Pages/Manage/Questions.cshtml` + `.cs`
- Create: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` + `.cs`

- [ ] **Step 1: Create Questions list page**

`src/Examageddon.Web/Pages/Manage/Questions.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class QuestionsModel(ExamManagementService examService) : PageModel
{
    public Exam Exam { get; set; } = null!;
    public List<Question> Questions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var exam = await examService.GetExamAsync(id);
        if (exam is null) return NotFound();
        Exam = exam;
        Questions = await examService.GetQuestionsAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int questionId)
    {
        await examService.DeleteQuestionAsync(questionId);
        return RedirectToPage(new { id });
    }
}
```

`src/Examageddon.Web/Pages/Manage/Questions.cshtml`:
```razor
@page "/manage/exams/{id:int}/questions"
@model Examageddon.Web.Pages.Manage.QuestionsModel
@{ ViewData["Title"] = $"Questions — {Model.Exam.Title}"; }

<div class="flex items-center justify-between mb-8">
    <div>
        <a href="/manage" class="text-arcade-muted text-sm hover:text-arcade-text">← Manage</a>
        <h1 class="text-3xl font-black mt-1">@Model.Exam.Title</h1>
        <p class="text-arcade-muted">@Model.Questions.Count questions</p>
    </div>
    <a href="/manage/exams/@Model.Exam.Id/questions/add"
       class="px-5 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg hover:opacity-90 transition">
        + Add Question
    </a>
</div>

@if (!Model.Questions.Any())
{
    <div class="bg-arcade-card border border-arcade-border rounded-xl p-8 text-center text-arcade-muted">
        No questions yet. Add one!
    </div>
}
else
{
    <div class="space-y-3">
        @foreach (var (q, i) in Model.Questions.Select((q, i) => (q, i)))
        {
            <div class="bg-arcade-card border border-arcade-border rounded-xl p-5 flex items-center gap-4">
                <span class="text-arcade-muted font-bold w-6 text-center">@(i+1)</span>
                <div class="flex-1">
                    <p class="font-medium">@q.Text</p>
                    <p class="text-arcade-muted text-sm">@q.AnswerOptions.Count options · @(q.ImageData != null ? "📷 has image" : "")</p>
                </div>
                <div class="flex gap-3">
                    <a href="/manage/exams/@Model.Exam.Id/questions/@q.Id/edit"
                       class="px-3 py-1.5 bg-arcade-dark border border-arcade-border rounded-lg text-sm hover:border-fire transition">Edit</a>
                    <form method="post" asp-page-handler="Delete" asp-route-id="@Model.Exam.Id" asp-route-questionId="@q.Id"
                          onsubmit="return confirm('Delete this question?')">
                        <button type="submit" class="px-3 py-1.5 bg-arcade-dark border border-red-800 rounded-lg text-sm text-red-400 hover:border-red-500 transition">
                            Delete
                        </button>
                    </form>
                </div>
            </div>
        }
    </div>
}
```

- [ ] **Step 2: Create QuestionForm page**

`src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml.cs`:
```csharp
using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class QuestionFormModel(ExamManagementService examService) : PageModel
{
    [BindProperty] public int ExamId { get; set; }
    [BindProperty] public string QuestionText { get; set; } = string.Empty;
    [BindProperty] public List<string> OptionTexts { get; set; } = ["", "", "", ""];
    [BindProperty] public int CorrectOptionIndex { get; set; }
    [BindProperty] public IFormFile? Image { get; set; }

    public int? EditQuestionId { get; set; }
    public Question? ExistingQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int? questionId)
    {
        ExamId = id;
        if (questionId.HasValue)
        {
            EditQuestionId = questionId;
            ExistingQuestion = await examService.GetQuestionAsync(questionId.Value);
            if (ExistingQuestion is null) return NotFound();
            QuestionText = ExistingQuestion.Text;
            OptionTexts = ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.Text).ToList();
            CorrectOptionIndex = ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).ToList().FindIndex(a => a.IsCorrect);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, int? questionId)
    {
        var options = OptionTexts
            .Select((text, idx) => new AnswerOption { Text = text, IsCorrect = idx == CorrectOptionIndex, OrderIndex = idx })
            .Where(a => !string.IsNullOrWhiteSpace(a.Text))
            .ToList();

        byte[]? imageData = null;
        string? imageContentType = null;
        if (Image is { Length: > 0 })
        {
            using var ms = new MemoryStream();
            await Image.CopyToAsync(ms);
            imageData = ms.ToArray();
            imageContentType = Image.ContentType;
        }

        if (questionId.HasValue)
        {
            var existing = await examService.GetQuestionAsync(questionId.Value);
            if (existing is null) return NotFound();
            existing.Text = QuestionText;
            existing.AnswerOptions = options;
            if (imageData is not null) { existing.ImageData = imageData; existing.ImageContentType = imageContentType; }
            await examService.UpdateQuestionAsync(existing);
        }
        else
        {
            var question = new Question
            {
                ExamId = id, Text = QuestionText, QuestionType = QuestionType.MultipleChoice,
                OrderIndex = await examService.GetQuestionsAsync(id) is { } qs ? qs.Count : 0,
                ImageData = imageData, ImageContentType = imageContentType,
                AnswerOptions = options
            };
            await examService.AddQuestionAsync(question);
        }

        return RedirectToPage("/Manage/Questions", new { id });
    }
}
```

`src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`:
```razor
@page "/manage/exams/{id:int}/questions/{questionId:int?}/edit"
@model Examageddon.Web.Pages.Manage.QuestionFormModel
@{ ViewData["Title"] = Model.EditQuestionId.HasValue ? "Edit Question" : "Add Question"; }

<h1 class="text-3xl font-black mb-8">@ViewData["Title"]</h1>

<form method="post" enctype="multipart/form-data" class="space-y-6 max-w-2xl">
    <input type="hidden" asp-for="ExamId" />

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Question Text *</label>
        <textarea asp-for="QuestionText" rows="3" required
            class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire"></textarea>
    </div>

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Image (optional)</label>
        @if (Model.ExistingQuestion?.ImageData != null)
        {
            <img src="/images/question/@Model.ExistingQuestion.Id" class="rounded-lg max-h-40 object-contain mb-3 border border-arcade-border" id="img-preview" />
        }
        else
        {
            <img id="img-preview" class="hidden rounded-lg max-h-40 object-contain mb-3 border border-arcade-border" />
        }
        <input type="file" asp-for="Image" accept="image/*"
            class="text-arcade-muted text-sm"
            onchange="previewImage(this)" />
    </div>

    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-3">Answer Options (mark the correct one)</label>
        <div class="space-y-3">
            @for (int i = 0; i < Model.OptionTexts.Count; i++)
            {
                <div class="flex items-center gap-3">
                    <input type="radio" name="CorrectOptionIndex" value="@i"
                           @(Model.CorrectOptionIndex == i ? "checked" : "")
                           class="accent-yellow-500" />
                    <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
                           placeholder="Option @(i + 1)"
                           class="flex-1 bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire" />
                </div>
            }
        </div>
    </div>

    <div class="flex gap-4">
        <button type="submit"
            class="px-8 py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition">
            @(Model.EditQuestionId.HasValue ? "Save Question" : "Add Question")
        </button>
        <a href="/manage/exams/@Model.ExamId/questions"
           class="px-8 py-3 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition">
            Cancel
        </a>
    </div>
</form>

@section Scripts {
<script>
function previewImage(input) {
    const preview = document.getElementById('img-preview');
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = e => { preview.src = e.target.result; preview.classList.remove('hidden'); };
        reader.readAsDataURL(input.files[0]);
    }
}
</script>
}
```

- [ ] **Step 3: Update links in Questions.cshtml to use correct routes**

The "Add Question" button uses `questionId = 0` as sentinel for "create new". Edit links use the actual question ID:

In `Questions.cshtml`, update the Add Question link:
```razor
<a href="/manage/exams/@Model.Exam.Id/questions/0/edit"
   class="px-5 py-2 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-lg hover:opacity-90 transition">
    + Add Question
</a>
```

And the edit link per question:
```razor
<a href="/manage/exams/@Model.Exam.Id/questions/@q.Id/edit" ...>Edit</a>
```

In `QuestionForm.cshtml.cs`, update both `OnGetAsync` and `OnPostAsync` to treat `questionId == 0` as create-new (the `{questionId:int?}` route parameter will be `0` when passed explicitly):
```csharp
public async Task<IActionResult> OnGetAsync(int id, int? questionId)
{
    ExamId = id;
    if (questionId.HasValue && questionId.Value != 0)
    {
        EditQuestionId = questionId;
        ExistingQuestion = await examService.GetQuestionAsync(questionId.Value);
        if (ExistingQuestion is null) return NotFound();
        QuestionText = ExistingQuestion.Text;
        OptionTexts = ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.Text).ToList();
        CorrectOptionIndex = ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).ToList().FindIndex(a => a.IsCorrect);
    }
    return Page();
}
```

And in `OnPostAsync`, replace `if (questionId.HasValue)` with `if (questionId.HasValue && questionId.Value != 0)`.

- [ ] **Step 4: Build**

```bash
dotnet build Examageddon.sln
```
Expected: Build succeeded.

- [ ] **Step 5: Run all tests**

```bash
dotnet test tests/Examageddon.Tests/ -v minimal
```
Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/
git commit -m "feat: add question CRUD management pages"
```

---

## Task 18: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md with build/run/test commands and architecture**

Replace contents of `CLAUDE.md` with:

```markdown
# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build Examageddon.sln

# Run the web app (starts on https://localhost:5001)
dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj

# Run all tests
dotnet test tests/Examageddon.Tests/

# Run a single test class
dotnet test tests/Examageddon.Tests/ --filter "ExamSessionServiceTests"

# Run a single test
dotnet test tests/Examageddon.Tests/ --filter "ExamSessionServiceTests.CompleteSession_CalculatesPassFail"
```

## Architecture

4 projects in `src/` + 1 test project in `tests/`:

- **Examageddon.Data** — EF Core entities, enums, `ExamageddonDbContext`. SQLite database created via `EnsureCreated()` on startup (no migrations CLI needed in dev).
- **Examageddon.Services** — Business logic. `PersonService`, `ExamManagementService`, `ExamSessionService`, `HistoryService`. DTOs live in `Models/`. No web dependencies.
- **Examageddon.Web** — Razor Pages + HTMX. Tailwind CSS and HTMX loaded via CDN (no build step). Session cookie holds `PersonId` and `PersonName`. Image blobs served via a minimal API endpoint at `/images/question/{id}`.
- **Examageddon.Tests** — XUnit. Uses SQLite `:memory:` via `TestDbContextFactory`. Tests cover all services.

### Key flows

**Exam session:** `POST /exams/{id}/setup` → creates `ExamSession` + `SessionQuestion` rows → redirects to `/sessions/{id}/question/1`. Each answer POSTs via HTMX and returns a partial (`_AnswerFeedback.cshtml`) that replaces the answer list.

**Feedback modes:** Direct = answer locks immediately with color, Next button injects via HTMX. AtEnd = free Back/Next navigation, Review page before submit.

**Database:** SQLite file at `src/Examageddon.Web/examageddon.db`. Created automatically on first run.
```

- [ ] **Step 2: Final build and test**

```bash
dotnet build Examageddon.sln && dotnet test tests/Examageddon.Tests/ -v minimal
```
Expected: Build succeeded, all tests pass.

- [ ] **Step 3: Final commit**

```bash
git add .
git commit -m "feat: complete Examageddon implementation"
```
