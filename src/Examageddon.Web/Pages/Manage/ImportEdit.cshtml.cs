using Examageddon.Data.Enums;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ImportEditModel : PageModel
{
    [BindProperty]
    public int ExamId { get; set; }

    [BindProperty]
    public int Index { get; set; }

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

    public IActionResult OnGet(int id, int index)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null || index < 0 || index >= staged.Count)
        {
            return RedirectToPage("/Manage/Import", new { id });
        }

        var q = staged[index];
        ExamId = id;
        Index = index;
        QuestionType = q.QuestionType;
        QuestionText = q.Text;
        AllowMultipleAnswers = q.AllowMultipleAnswers;
        ScoringMode = q.ScoringMode;
        OptionTexts = [.. q.Options.Select(o => o.Text)];
        MatchTexts = [.. q.Options.Select(o => o.MatchText ?? string.Empty)];
        CorrectOptionIndices = [.. q.Options.Select((o, i) => (o, i)).Where(x => x.o.IsCorrect).Select(x => x.i)];
        return Page();
    }

    public IActionResult OnPost(int id, int index)
    {
        var options = OptionTexts
            .Select((text, idx) => new StagedOption
            {
                Text = text,
                MatchText = QuestionType == QuestionType.DragAndDrop && idx < MatchTexts.Count
                    ? MatchTexts[idx]
                    : null,
                IsCorrect = QuestionType == QuestionType.DragAndDrop
                    ? !string.IsNullOrWhiteSpace(text) && idx < MatchTexts.Count && !string.IsNullOrWhiteSpace(MatchTexts[idx])
                    : CorrectOptionIndices.Contains(idx),
            })
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();

        if (QuestionType == QuestionType.DragAndDrop)
        {
            if (options.Count == 0 || options.Any(o => string.IsNullOrWhiteSpace(o.MatchText)))
            {
                ModelState.AddModelError(string.Empty, "Each pair must have both a label and a sentence.");
                ExamId = id;
                Index = index;
                return Page();
            }
        }
        else if (QuestionType == QuestionType.Statement)
        {
            if (options.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one statement is required.");
                ExamId = id;
                Index = index;
                return Page();
            }
        }
        else if (!options.Any(o => o.IsCorrect))
        {
            ModelState.AddModelError(string.Empty, "At least one answer option must be marked as correct.");
            ExamId = id;
            Index = index;
            return Page();
        }

        var forceMultiple = QuestionType is QuestionType.DragAndDrop or QuestionType.Statement;

        var updated = new StagedQuestion
        {
            QuestionType = QuestionType,
            Text = QuestionText,
            AllowMultipleAnswers = forceMultiple || AllowMultipleAnswers,
            ScoringMode = ScoringMode,
            Options = options,
        };

        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null || index < 0 || index >= staged.Count)
        {
            return RedirectToPage("/Manage/Questions", new { id });
        }

        staged[index] = updated;
        ImportSession.Set(HttpContext.Session, id, staged);
        return RedirectToPage("/Manage/Import", new { id });
    }
}
