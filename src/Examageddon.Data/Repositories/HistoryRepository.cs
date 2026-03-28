using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data.Repositories;

internal sealed class HistoryRepository(ExamageddonDbContext db) : IHistoryRepository
{
    public Task<List<ExamSession>> GetCompletedByPersonAsync(int personId)
    {
        return db.ExamSessions
            .Include(s => s.Exam)
            .Where(s => s.PersonId == personId && s.CompletedAt != null)
            .OrderByDescending(s => s.CompletedAt)
            .ToListAsync();
    }
}
