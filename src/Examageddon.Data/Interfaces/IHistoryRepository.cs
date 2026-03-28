using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IHistoryRepository
{
    Task<List<ExamSession>> GetCompletedByPersonAsync(int personId);
}
