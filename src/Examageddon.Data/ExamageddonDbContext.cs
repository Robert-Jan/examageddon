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

    public DbSet<SessionAnswerSelection> SessionAnswerSelections => Set<SessionAnswerSelection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Person>()
            .HasIndex(p => p.Name).IsUnique();

        modelBuilder.Entity<SessionAnswer>()
            .HasIndex(sa => new { sa.SessionId, sa.QuestionId }).IsUnique();
    }
}
