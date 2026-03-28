using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages;

public class IndexModel(PersonService personService, ExamSessionService sessionService) : PageModel
{
    public List<Person> People { get; set; } = [];

    [BindProperty]
    public string NewName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var personId = HttpContext.Session.GetInt32(SessionKeys.PersonId);
        if (personId is not null)
        {
            var active = await sessionService.GetActiveSessionAsync(personId.Value);
            if (active is not null)
            {
                return RedirectToPage("/Sessions/Gate");
            }
        }

        People = await personService.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(string name)
    {
        var person = await personService.GetOrCreateAsync(name);
        HttpContext.Session.SetInt32(SessionKeys.PersonId, person.Id);
        HttpContext.Session.SetString(SessionKeys.PersonName, person.Name);

        var active = await sessionService.GetActiveSessionAsync(person.Id);
        if (active is not null)
        {
            return RedirectToPage("/Sessions/Gate");
        }

        return RedirectToPage("/Exams/Index");
    }

    public async Task<IActionResult> OnPostNewAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            return RedirectToPage();
        }

        var person = await personService.GetOrCreateAsync(NewName.Trim());
        HttpContext.Session.SetInt32(SessionKeys.PersonId, person.Id);
        HttpContext.Session.SetString(SessionKeys.PersonName, person.Name);

        var active = await sessionService.GetActiveSessionAsync(person.Id);
        if (active is not null)
        {
            return RedirectToPage("/Sessions/Gate");
        }

        return RedirectToPage("/Exams/Index");
    }
}
