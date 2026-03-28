using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class IndexModel(ExamManagementService examService) : PageModel
{
    public List<Exam> Exams { get; set; } = [];

    public Dictionary<int, int> QuestionCounts { get; set; } = [];

    public async Task OnGetAsync()
    {
        Exams = await examService.GetAllExamsAsync();
        QuestionCounts = await examService.GetQuestionCountsAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await examService.DeleteExamAsync(id);
        return RedirectToPage();
    }
}
