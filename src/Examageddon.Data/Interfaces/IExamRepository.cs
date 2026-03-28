using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IExamRepository
{
    Task<List<Exam>> GetAllAsync();

    Task<Dictionary<int, int>> GetQuestionCountsAsync();

    Task<Exam?> GetByIdWithQuestionsAsync(int id);

    Task<Exam> AddAsync(Exam exam);

    Task UpdateAsync(Exam exam);

    Task DeleteAsync(int id);

    Task<List<Question>> GetQuestionsAsync(int examId);

    Task<Question?> GetQuestionAsync(int questionId);

    Task<Question> AddQuestionAsync(Question question);

    Task UpdateQuestionAsync(Question question);

    Task DeleteQuestionAsync(int questionId);

    Task SaveImageAsync(int questionId, byte[] data, string contentType);

    Task<(byte[]? Data, string? ContentType)> GetImageAsync(int questionId);
}
