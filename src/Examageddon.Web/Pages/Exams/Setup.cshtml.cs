using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Exams;

public class SetupModel(ExamManagementService examService, ExamSessionService sessionService) : PageModel
{
    public Exam Exam { get; set; } = null!;

    [BindProperty]
    public QuestionMode QuestionMode { get; set; } = QuestionMode.All;

    [BindProperty]
    public OrderMode OrderMode { get; set; } = OrderMode.Default;

    [BindProperty]
    public FeedbackMode FeedbackMode { get; set; } = FeedbackMode.Direct;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        var exam = await examService.GetExamAsync(id);
        if (exam is null)
        {
            return NotFound();
        }

        Exam = exam;

        if (exam.ScoringPreset == ScoringPreset.AzureCertification)
        {
            QuestionMode = QuestionMode.Limited;
            OrderMode = OrderMode.Random;
            FeedbackMode = FeedbackMode.AtEnd;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        var exam = await examService.GetExamAsync(id);
        if (exam is null)
        {
            return NotFound();
        }

        var opts = new Services.Models.SessionOptions { QuestionMode = QuestionMode, OrderMode = OrderMode, FeedbackMode = FeedbackMode };
        var session = await sessionService.CreateSessionAsync(personId.Value, id, opts);
        return RedirectToPage("/Sessions/Question", new { sessionId = session.Id, n = 1 });
    }
}
