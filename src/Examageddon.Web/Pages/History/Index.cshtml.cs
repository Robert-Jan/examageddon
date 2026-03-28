using Examageddon.Services;
using Examageddon.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.History;

public class IndexModel(HistoryService historyService) : PageModel
{
    public List<HistoryEntryModel> Entries { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is null)
        {
            return RedirectToPage("/Index");
        }

        Entries = await historyService.GetHistoryForPersonAsync(personId.Value);
        return Page();
    }
}
