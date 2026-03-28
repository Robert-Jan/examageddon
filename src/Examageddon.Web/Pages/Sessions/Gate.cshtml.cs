using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class GateModel(ExamSessionService sessionService) : PageModel
{
    public string ExamTitle { get; set; } = string.Empty;

    public int SessionId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        var session = await sessionService.GetActiveSessionAsync(personId.Value);
        if (session is null)
        {
            return RedirectToPage("/Exams/Index");
        }

        ExamTitle = session.Exam.Title;
        SessionId = session.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostContinueAsync(int sessionId)
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        var session = await sessionService.GetActiveSessionAsync(personId.Value);
        if (session is null || session.Id != sessionId)
        {
            return RedirectToPage("/Exams/Index");
        }

        var n = await sessionService.GetResumePositionAsync(sessionId);
        return RedirectToPage("/Sessions/Question", new { sessionId, n });
    }

    public async Task<IActionResult> OnPostAbandonAsync(int sessionId)
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        await sessionService.AbandonSessionAsync(sessionId, personId.Value);
        return RedirectToPage("/Exams/Index");
    }
}
