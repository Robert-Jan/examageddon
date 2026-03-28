using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data.Repositories;

internal sealed class ExamSessionRepository(ExamageddonDbContext db) : IExamSessionRepository
{
    public async Task<ExamSession> CreateAsync(ExamSession session, IReadOnlyList<SessionQuestion> sessionQuestions)
    {
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();

        foreach (var sq in sessionQuestions)
        {
            sq.SessionId = session.Id;
        }

        db.SessionQuestions.AddRange(sessionQuestions);
        await db.SaveChangesAsync();

        return session;
    }

    public Task<ExamSession?> GetByIdAsync(int sessionId)
    {
        return db.ExamSessions.FindAsync(sessionId).AsTask();
    }

    public Task<SessionQuestion?> GetSessionQuestionAsync(int sessionId, int position)
    {
        return db.SessionQuestions
            .Include(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(sq => sq.SessionId == sessionId && sq.Position == position);
    }

    public Task<SessionAnswer?> GetAnswerAsync(int sessionId, int questionId)
    {
        return db.SessionAnswers
            .Include(sa => sa.Selections)
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.QuestionId == questionId);
    }

    public Task<List<AnswerOption>> GetAnswerOptionsAsync(IEnumerable<int> answerOptionIds)
    {
        var ids = answerOptionIds.ToList();
        return db.AnswerOptions.Where(a => ids.Contains(a.Id)).ToListAsync();
    }

    public async Task AddAnswerAsync(SessionAnswer answer)
    {
        db.SessionAnswers.Add(answer);
        await db.SaveChangesAsync();
    }

    public Task<ExamSession?> GetWithExamAndAnswersAsync(int sessionId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionAnswers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public Task<ExamSession?> GetFullSessionAsync(int sessionId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionQuestions).ThenInclude(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Include(s => s.SessionAnswers).ThenInclude(sa => sa.Selections).ThenInclude(sel => sel.AnswerOption)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId)
    {
        return db.SessionQuestions
            .Include(sq => sq.Question).ThenInclude(q => q.AnswerOptions)
            .Where(sq => sq.SessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToListAsync();
    }

    public Task SaveChangesAsync()
    {
        return db.SaveChangesAsync();
    }

    public Task<ExamSession?> GetActiveByPersonAsync(int personId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Where(s => s.PersonId == personId && s.CompletedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetFirstUnansweredPositionAsync(int sessionId)
    {
        return db.SessionQuestions
            .Where(sq => sq.SessionId == sessionId &&
                         !db.SessionAnswers.Any(sa => sa.SessionId == sessionId && sa.QuestionId == sq.QuestionId))
            .OrderBy(sq => sq.Position)
            .Select(sq => (int?)sq.Position)
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
}
