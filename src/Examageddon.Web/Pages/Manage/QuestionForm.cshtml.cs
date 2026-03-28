using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class QuestionFormModel(ExamManagementService examService) : PageModel
{
    [BindProperty]
    public int ExamId { get; set; }

    [BindProperty]
    public QuestionType QuestionType { get; set; }

    [BindProperty]
    public string QuestionText { get; set; } = string.Empty;

    [BindProperty]
    public bool AllowMultipleAnswers { get; set; }

    [BindProperty]
    public MultiAnswerScoringMode ScoringMode { get; set; }

    [BindProperty]
    public List<string> OptionTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];

    [BindProperty]
    public List<string> MatchTexts { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];

    [BindProperty]
    public List<int> CorrectOptionIndices { get; set; } = [];

    [BindProperty]
    public IFormFile? Image { get; set; }

    public int? EditQuestionId { get; set; }

    public Question? ExistingQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int? questionId)
    {
        ExamId = id;
        if (questionId.HasValue && questionId.Value != 0)
        {
            EditQuestionId = questionId;
            ExistingQuestion = await examService.GetQuestionAsync(questionId.Value);
            if (ExistingQuestion is null)
            {
                return NotFound();
            }

            QuestionText = ExistingQuestion.Text;
            QuestionType = ExistingQuestion.QuestionType;
            AllowMultipleAnswers = ExistingQuestion.AllowMultipleAnswers;
            ScoringMode = ExistingQuestion.ScoringMode;
            OptionTexts = [.. ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.Text)];
            MatchTexts = [.. ExistingQuestion.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => a.MatchText ?? string.Empty)];
            CorrectOptionIndices = [.. ExistingQuestion.AnswerOptions
                .OrderBy(a => a.OrderIndex)
                .Select((a, i) => (a, i))
                .Where(x => x.a.IsCorrect)
                .Select(x => x.i)];
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, int? questionId)
    {
        var options = OptionTexts
            .Select((text, idx) => new AnswerOption
            {
                Text = text,
                MatchText = QuestionType == QuestionType.DragAndDrop && idx < MatchTexts.Count
                    ? MatchTexts[idx]
                    : null,
                IsCorrect = QuestionType == QuestionType.DragAndDrop
                    ? !string.IsNullOrWhiteSpace(text) && idx < MatchTexts.Count && !string.IsNullOrWhiteSpace(MatchTexts[idx])
                    : CorrectOptionIndices.Contains(idx),
                OrderIndex = idx,
            })
            .Where(a => !string.IsNullOrWhiteSpace(a.Text))
            .ToList();

        if (QuestionType == QuestionType.DragAndDrop)
        {
            if (options.Count == 0 || options.Any(o => string.IsNullOrWhiteSpace(o.MatchText)))
            {
                ModelState.AddModelError(string.Empty, "Each pair must have both a label and a sentence.");
                ExamId = id;
                return Page();
            }

            AllowMultipleAnswers = true;
        }
        else if (QuestionType == QuestionType.Statement)
        {
            if (options.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one statement is required.");
                ExamId = id;
                return Page();
            }

            AllowMultipleAnswers = true;
        }
        else if (!options.Any(o => o.IsCorrect))
        {
            ModelState.AddModelError(string.Empty, "At least one answer option must be marked as correct.");
            ExamId = id;
            return Page();
        }

        byte[]? imageData = null;
        string? imageContentType = null;
        if (Image is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await Image.CopyToAsync(ms);
            imageData = ms.ToArray();
            imageContentType = Image.ContentType;
        }

        if (questionId.HasValue && questionId.Value != 0)
        {
            var existing = await examService.GetQuestionAsync(questionId.Value);
            if (existing is null)
            {
                return NotFound();
            }

            existing.Text = QuestionText;
            existing.QuestionType = QuestionType;
            existing.AllowMultipleAnswers = AllowMultipleAnswers;
            existing.ScoringMode = ScoringMode;
            existing.AnswerOptions = options;
            if (imageData is not null)
            {
                existing.ImageData = imageData;
                existing.ImageContentType = imageContentType;
            }

            await examService.UpdateQuestionAsync(existing);
        }
        else
        {
            var qs = await examService.GetQuestionsAsync(id);
            var question = new Question
            {
                ExamId = id,
                Text = QuestionText,
                QuestionType = QuestionType,
                AllowMultipleAnswers = AllowMultipleAnswers,
                ScoringMode = ScoringMode,
                OrderIndex = qs.Count,
                ImageData = imageData,
                ImageContentType = imageContentType,
                AnswerOptions = options,
            };
            await examService.AddQuestionAsync(question);
        }

        return RedirectToPage("/Manage/Questions", new { id });
    }
}
