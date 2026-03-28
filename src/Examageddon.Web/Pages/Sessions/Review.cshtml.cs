using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class ReviewModel(ExamSessionService sessionService) : PageModel
{
    public SessionResultModel Result { get; set; } = null!;

    public int SessionId { get; set; }

    public async Task<IActionResult> OnGetAsync(int sessionId)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        SessionId = sessionId;
        Result = await sessionService.GetResultAsync(sessionId);
        return Page();
    }
}
