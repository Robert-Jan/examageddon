using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;

namespace Examageddon.Services;

public class ExamManagementService(IExamRepository examRepository)
{
    public Task<List<Exam>> GetAllExamsAsync()
    {
        return examRepository.GetAllAsync();
    }

    public Task<Dictionary<int, int>> GetQuestionCountsAsync()
    {
        return examRepository.GetQuestionCountsAsync();
    }

    public Task<Exam?> GetExamAsync(int id)
    {
        return examRepository.GetByIdWithQuestionsAsync(id);
    }

    public Task<Exam> CreateExamAsync(Exam exam)
    {
        return examRepository.AddAsync(exam);
    }

    public Task UpdateExamAsync(Exam exam)
    {
        return examRepository.UpdateAsync(exam);
    }

    public Task DeleteExamAsync(int id)
    {
        return examRepository.DeleteAsync(id);
    }

    public Task<List<Question>> GetQuestionsAsync(int examId)
    {
        return examRepository.GetQuestionsAsync(examId);
    }

    public Task<Question?> GetQuestionAsync(int questionId)
    {
        return examRepository.GetQuestionAsync(questionId);
    }

    public Task<Question> AddQuestionAsync(Question question)
    {
        return examRepository.AddQuestionAsync(question);
    }

    public Task UpdateQuestionAsync(Question question)
    {
        return examRepository.UpdateQuestionAsync(question);
    }

    public Task DeleteQuestionAsync(int questionId)
    {
        return examRepository.DeleteQuestionAsync(questionId);
    }

    public Task SaveImageAsync(int questionId, byte[] data, string contentType)
    {
        return examRepository.SaveImageAsync(questionId, data, contentType);
    }

    public Task<(byte[]? Data, string? ContentType)> GetImageAsync(int questionId)
    {
        return examRepository.GetImageAsync(questionId);
    }
}
