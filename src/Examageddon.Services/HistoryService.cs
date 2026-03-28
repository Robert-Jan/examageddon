using Examageddon.Data.Interfaces;
using Examageddon.Services.Models;

namespace Examageddon.Services;

public class HistoryService(IHistoryRepository historyRepository)
{
    public async Task<List<HistoryEntryModel>> GetHistoryForPersonAsync(int personId)
    {
        var sessions = await historyRepository.GetCompletedByPersonAsync(personId);
        return [.. sessions.Select(s => new HistoryEntryModel
        {
            SessionId = s.Id,
            ExamTitle = s.Exam.Title,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt!.Value,
            TotalQuestions = s.TotalQuestions,
            CorrectAnswers = s.CorrectAnswers,
            IsPassed = s.IsPassed ?? false,
        })];
    }
}
