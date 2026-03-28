using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IExamSessionRepository
{
    Task<ExamSession> CreateAsync(ExamSession session, IReadOnlyList<SessionQuestion> sessionQuestions);

    Task<ExamSession?> GetByIdAsync(int sessionId);

    Task<SessionQuestion?> GetSessionQuestionAsync(int sessionId, int position);

    Task<SessionAnswer?> GetAnswerAsync(int sessionId, int questionId);

    Task<List<AnswerOption>> GetAnswerOptionsAsync(IEnumerable<int> answerOptionIds);

    Task AddAnswerAsync(SessionAnswer answer);

    Task<ExamSession?> GetWithExamAndAnswersAsync(int sessionId);

    Task<ExamSession?> GetFullSessionAsync(int sessionId);

    Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId);

    Task SaveChangesAsync();

    Task<ExamSession?> GetActiveByPersonAsync(int personId);

    Task<int?> GetFirstUnansweredPositionAsync(int sessionId);

    Task DeleteAsync(int sessionId);
}
