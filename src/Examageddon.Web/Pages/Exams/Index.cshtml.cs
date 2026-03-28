using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Exams;

public class IndexModel(ExamManagementService examService) : PageModel
{
    public List<Exam> Exams { get; set; } = [];

    public Dictionary<int, int> QuestionCounts { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (HttpContext.Session.GetInt32(SessionKeys.PersonId) is null)
        {
            return RedirectToPage("/Index");
        }

        Exams = await examService.GetAllExamsAsync();
        QuestionCounts = await examService.GetQuestionCountsAsync();
        return Page();
    }
}
