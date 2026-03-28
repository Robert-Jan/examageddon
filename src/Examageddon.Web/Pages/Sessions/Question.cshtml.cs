using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class QuestionModel(ExamSessionService sessionService) : PageModel
{
    public SessionQuestionModel? SessionQuestion { get; set; }

    public async Task<IActionResult> OnGetAsync(int sessionId, int n, bool fromReview = false)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n, fromReview);
        if (SessionQuestion is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAnswerAsync(int sessionId, int n, int questionId, List<int> answerIds)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        await sessionService.SubmitAnswerAsync(sessionId, questionId, answerIds);
        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n);
        if (SessionQuestion is null)
        {
            return NotFound();
        }

        return Partial("_AnswerFeedback", SessionQuestion);
    }

    public async Task<IActionResult> OnPostDragAnswerAsync(int sessionId, int n, int questionId, List<int> slotIds, List<int> placedIds)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        await sessionService.SubmitDragAnswerAsync(sessionId, questionId, slotIds, placedIds);
        SessionQuestion = await sessionService.GetSessionQuestionAsync(sessionId, n);
        if (SessionQuestion is null)
        {
            return NotFound();
        }

        return Partial("_AnswerFeedback", SessionQuestion);
    }
}
