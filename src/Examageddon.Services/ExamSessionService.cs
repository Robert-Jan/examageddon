using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Data.Interfaces;
using Examageddon.Services.Models;

namespace Examageddon.Services;

public class ExamSessionService(IExamRepository examRepo, IExamSessionRepository sessionRepo)
{
    public async Task<ExamSession> CreateSessionAsync(int personId, int examId, SessionOptions opts)
    {
        var exam = await examRepo.GetByIdWithQuestionsAsync(examId)
            ?? throw new InvalidOperationException($"Exam {examId} not found.");

        var questions = exam.Questions.OrderBy(q => q.OrderIndex).ToList();
        if (opts.QuestionMode == QuestionMode.Limited)
        {
            questions = [.. questions.Take(exam.ExamQuestionCount)];
        }

        if (opts.OrderMode == OrderMode.Random)
        {
            questions = [.. questions.OrderBy(_ => Guid.NewGuid())];
        }

        var session = new ExamSession
        {
            PersonId = personId,
            ExamId = examId,
            StartedAt = DateTime.UtcNow,
            QuestionMode = opts.QuestionMode,
            OrderMode = opts.OrderMode,
            FeedbackMode = opts.FeedbackMode,
            ScoringPreset = exam.ScoringPreset,
            TotalQuestions = questions.Count,
        };

        var sessionQuestions = questions
            .Select((q, i) => new SessionQuestion { QuestionId = q.Id, Position = i + 1 })
            .ToList();

        return await sessionRepo.CreateAsync(session, sessionQuestions);
    }

    public async Task<SessionQuestionModel?> GetSessionQuestionAsync(int sessionId, int position, bool fromReview = false)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            return null;
        }

        var sq = await sessionRepo.GetSessionQuestionAsync(sessionId, position);
        if (sq is null)
        {
            return null;
        }

        var answer = await sessionRepo.GetAnswerAsync(sessionId, sq.QuestionId);
        var selectedIds = answer?.Selections.Select(s => s.AnswerOptionId).ToHashSet() ?? [];

        return new SessionQuestionModel
        {
            SessionId = sessionId,
            Position = position,
            TotalQuestions = session.TotalQuestions,
            FeedbackMode = session.FeedbackMode,
            Question = sq.Question,
            AnswerOptions = sq.Question.QuestionType == QuestionType.DragAndDrop
                ? [.. sq.Question.AnswerOptions.OrderBy(_ => Guid.NewGuid())]
                : [.. sq.Question.AnswerOptions.OrderBy(a => a.OrderIndex)],
            SelectedAnswerOptionIds = selectedIds,
            IsAnswered = answer is not null,
            AllowMultipleAnswers = sq.Question.AllowMultipleAnswers,
            QuestionType = sq.Question.QuestionType,
            IsFromReview = fromReview,
            DragPlacements = sq.Question.QuestionType == QuestionType.DragAndDrop && answer is not null
                ? answer.Selections
                    .Where(s => s.PlacedOptionId.HasValue)
                    .ToDictionary(s => s.AnswerOptionId, s => s.PlacedOptionId!.Value)
                : [],
        };
    }

    public async Task SubmitAnswerAsync(int sessionId, int questionId, IReadOnlyList<int> answerOptionIds)
    {
        var question = await examRepo.GetQuestionAsync(questionId);
        if (question is null)
        {
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        var isAzure = session?.ScoringPreset == ScoringPreset.AzureCertification;

        var selectedIds = answerOptionIds.ToHashSet();
        var correctIds = question.AnswerOptions.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

        double score;
        bool isCorrect;

        if (!question.AllowMultipleAnswers || question.ScoringMode == MultiAnswerScoringMode.AllOrNothing)
        {
            isCorrect = selectedIds.SetEquals(correctIds);
            score = isCorrect ? 1.0 : 0.0;
        }
        else if (isAzure)
        {
            // Azure: each correct selection earns its point; wrong picks are ignored (no deduction)
            var correctSelected = selectedIds.Count(correctIds.Contains);
            score = correctIds.Count == 0
                ? 0.0
                : (double)correctSelected / correctIds.Count;
            isCorrect = score > 0;
        }
        else
        {
            var correctSelected = selectedIds.Count(correctIds.Contains);
            var wrongSelected = selectedIds.Count(id => !correctIds.Contains(id));
            score = correctIds.Count == 0
                ? 0.0
                : Math.Max(0.0, (correctSelected - wrongSelected) / (double)correctIds.Count);
            isCorrect = score > 0;
        }

        var selections = answerOptionIds
            .Select(id => new SessionAnswerSelection { AnswerOptionId = id })
            .ToList();

        var existing = await sessionRepo.GetAnswerAsync(sessionId, questionId);

        if (existing is not null)
        {
            existing.Score = score;
            existing.IsCorrect = isCorrect;
            existing.AnsweredAt = DateTime.UtcNow;
            existing.Selections.Clear();
            foreach (var sel in selections)
            {
                existing.Selections.Add(sel);
            }

            await sessionRepo.SaveChangesAsync();
        }
        else
        {
            await sessionRepo.AddAnswerAsync(new SessionAnswer
            {
                SessionId = sessionId,
                QuestionId = questionId,
                Score = score,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow,
                Selections = selections,
            });
        }
    }

    public async Task SubmitDragAnswerAsync(
        int sessionId,
        int questionId,
        IReadOnlyList<int> slotIds,
        IReadOnlyList<int> placedIds)
    {
        var question = await examRepo.GetQuestionAsync(questionId);
        if (question is null)
        {
            return;
        }

        // slotIds and placedIds are expected to have equal length (UI enforces all slots filled before submit)
        var totalPairs = slotIds.Count;

        // Correct when the placed label's option ID matches the slot's own option ID
        // (valid because DragAndDrop has no distractors — each slot IS its own correct answer)
        var correctCount = slotIds.Where((slotId, i) => i < placedIds.Count && placedIds[i] == slotId).Count();

        double score;
        bool isCorrect;

        if (question.ScoringMode == MultiAnswerScoringMode.AllOrNothing)
        {
            isCorrect = correctCount == totalPairs;
            score = isCorrect ? 1.0 : 0.0;
        }
        else
        {
            score = totalPairs == 0 ? 0.0 : (double)correctCount / totalPairs;
            isCorrect = score > 0;
        }

        var selections = slotIds
            .Select((slotId, i) => new SessionAnswerSelection
            {
                AnswerOptionId = slotId,
                PlacedOptionId = i < placedIds.Count ? placedIds[i] : null,
            })
            .ToList();

        var existing = await sessionRepo.GetAnswerAsync(sessionId, questionId);
        if (existing is not null)
        {
            existing.Score = score;
            existing.IsCorrect = isCorrect;
            existing.AnsweredAt = DateTime.UtcNow;
            existing.Selections.Clear();
            foreach (var sel in selections)
            {
                existing.Selections.Add(sel);
            }

            await sessionRepo.SaveChangesAsync();
        }
        else
        {
            await sessionRepo.AddAnswerAsync(new SessionAnswer
            {
                SessionId = sessionId,
                QuestionId = questionId,
                Score = score,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow,
                Selections = selections,
            });
        }
    }

    public async Task<SessionResultModel> CompleteSessionAsync(int sessionId)
    {
        var session = await sessionRepo.GetWithExamAndAnswersAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var totalScore = session.SessionAnswers.Sum(a => a.Score);
        session.CorrectAnswers = totalScore;

        if (session.ScoringPreset == ScoringPreset.AzureCertification)
        {
            var scaledScore = session.TotalQuestions == 0
                ? 0
                : (int)Math.Round(totalScore / session.TotalQuestions * 1000);
            session.IsPassed = scaledScore >= session.Exam.PassingThreshold;
        }
        else
        {
            var scorePercent = session.TotalQuestions == 0 ? 0 : totalScore / session.TotalQuestions * 100;
            session.IsPassed = scorePercent >= session.Exam.PassingThreshold;
        }

        session.CompletedAt = DateTime.UtcNow;
        await sessionRepo.SaveChangesAsync();

        return await GetResultAsync(sessionId);
    }

    public async Task<SessionResultModel> GetResultAsync(int sessionId)
    {
        var session = await sessionRepo.GetFullSessionAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var items = session.SessionQuestions.OrderBy(sq => sq.Position).Select(sq =>
        {
            var answer = session.SessionAnswers.FirstOrDefault(sa => sa.QuestionId == sq.QuestionId);
            var dragPlacements = sq.Question.QuestionType == QuestionType.DragAndDrop && answer is not null
                ? answer.Selections
                    .Where(s => s.PlacedOptionId.HasValue)
                    .ToDictionary(
                        s => s.AnswerOptionId,
                        s => sq.Question.AnswerOptions.FirstOrDefault(a => a.Id == s.PlacedOptionId!.Value)!)
                    .Where(kv => kv.Value is not null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
                : (IReadOnlyDictionary<int, AnswerOption>)new Dictionary<int, AnswerOption>();

            return new QuestionResultItem
            {
                Question = sq.Question,
                SelectedOptions = answer?.Selections.Select(s => s.AnswerOption).ToList() ?? [],
                IsCorrect = answer?.IsCorrect ?? false,
                IsAnswered = answer is not null,
                DragPlacements = dragPlacements,
            };
        }).ToList();

        return new SessionResultModel
        {
            SessionId = sessionId,
            ExamTitle = session.Exam.Title,
            TotalQuestions = session.TotalQuestions,
            CorrectAnswers = session.CorrectAnswers,
            PassingThreshold = session.Exam.PassingThreshold,
            IsPassed = session.IsPassed ?? false,
            ScoringPreset = session.ScoringPreset,
            QuestionResults = items,
        };
    }

    public Task<List<SessionQuestion>> GetReviewQuestionsAsync(int sessionId)
    {
        return sessionRepo.GetReviewQuestionsAsync(sessionId);
    }

    public Task<ExamSession?> GetActiveSessionAsync(int personId)
    {
        return sessionRepo.GetActiveByPersonAsync(personId);
    }

    public async Task<int> GetResumePositionAsync(int sessionId)
    {
        return await sessionRepo.GetFirstUnansweredPositionAsync(sessionId) ?? 1;
    }

    public async Task AbandonSessionAsync(int sessionId, int personId)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null || session.PersonId != personId)
        {
            return;
        }

        await sessionRepo.DeleteAsync(sessionId);
    }
}
