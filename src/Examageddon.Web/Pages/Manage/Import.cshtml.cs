using Examageddon.Data.Entities;
using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ImportModel(ExamManagementService examService) : PageModel
{
    public Exam Exam { get; set; } = null!;

    public List<StagedQuestion> StagedQuestions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null or { Count: 0 })
        {
            return RedirectToPage("/Manage/Questions", new { id });
        }

        var exam = await examService.GetExamAsync(id);
        if (exam is null)
        {
            return NotFound();
        }

        Exam = exam;
        StagedQuestions = staged;
        return Page();
    }

    public IActionResult OnPostRemove(int id, int index)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is not null && index >= 0 && index < staged.Count)
        {
            staged.RemoveAt(index);
            if (staged.Count == 0)
            {
                ImportSession.Clear(HttpContext.Session, id);
                return RedirectToPage("/Manage/Questions", new { id });
            }

            ImportSession.Set(HttpContext.Session, id, staged);
        }

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostCancel(int id)
    {
        ImportSession.Clear(HttpContext.Session, id);
        return RedirectToPage("/Manage/Questions", new { id });
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var staged = ImportSession.Get(HttpContext.Session, id);
        if (staged is null or { Count: 0 })
        {
            return RedirectToPage("/Manage/Questions", new { id });
        }

        var existing = await examService.GetQuestionsAsync(id);
        var baseIndex = existing.Count;

        for (var i = 0; i < staged.Count; i++)
        {
            var question = QuestionImportService.ToQuestion(staged[i], id, baseIndex + i);
            await examService.AddQuestionAsync(question);
        }

        ImportSession.Clear(HttpContext.Session, id);
        return RedirectToPage("/Manage/Questions", new { id });
    }
}
