using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data.Repositories;

internal sealed class ExamRepository(ExamageddonDbContext db) : IExamRepository
{
    public Task<List<Exam>> GetAllAsync()
    {
        return db.Exams.OrderBy(e => e.Title).ToListAsync();
    }

    public Task<Dictionary<int, int>> GetQuestionCountsAsync()
    {
        return db.Questions
            .GroupBy(q => q.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count);
    }

    public Task<Exam?> GetByIdWithQuestionsAsync(int id)
    {
        return db.Exams.Include(e => e.Questions).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Exam> AddAsync(Exam exam)
    {
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return exam;
    }

    public async Task UpdateAsync(Exam exam)
    {
        db.Exams.Update(exam);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is not null)
        {
            db.Exams.Remove(exam);
            await db.SaveChangesAsync();
        }
    }

    public Task<List<Question>> GetQuestionsAsync(int examId)
    {
        return db.Questions.Include(q => q.AnswerOptions)
            .Where(q => q.ExamId == examId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync();
    }

    public Task<Question?> GetQuestionAsync(int questionId)
    {
        return db.Questions.Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == questionId);
    }

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
        if (q is not null)
        {
            db.Questions.Remove(q);
            await db.SaveChangesAsync();
        }
    }

    public async Task SaveImageAsync(int questionId, byte[] data, string contentType)
    {
        var q = await db.Questions.FindAsync(questionId);
        if (q is null)
        {
            return;
        }

        q.ImageData = data;
        q.ImageContentType = contentType;
        await db.SaveChangesAsync();
    }

    public async Task<(byte[]? Data, string? ContentType)> GetImageAsync(int questionId)
    {
        var q = await db.Questions.FindAsync(questionId);
        return (q?.ImageData, q?.ImageContentType);
    }
}
