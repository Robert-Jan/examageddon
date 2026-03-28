using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Examageddon.Web.Pages.Manage;

public class ExamFormModel(ExamManagementService examService) : PageModel
{
    [BindProperty]
    public Exam Exam { get; set; } = new();

    public bool IsEdit => Exam.Id != 0;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id != 0)
        {
            var exam = await examService.GetExamAsync(id);
            if (exam is null)
            {
                return NotFound();
            }

            Exam = exam;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Exam.Id == 0)
        {
            await examService.CreateExamAsync(Exam);
        }
        else
        {
            await examService.UpdateExamAsync(Exam);
        }

        return RedirectToPage("/Manage/Index");
    }
}
