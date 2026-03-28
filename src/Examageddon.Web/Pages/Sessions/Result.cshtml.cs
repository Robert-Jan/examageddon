using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Sessions;

public class ResultModel(ExamSessionService sessionService) : PageModel
{
    public SessionResultModel Result { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int sessionId)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        Result = await sessionService.GetResultAsync(sessionId);
        return Page();
    }
}
