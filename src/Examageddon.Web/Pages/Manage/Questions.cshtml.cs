using Examageddon.Data.Entities;
using Examageddon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace Examageddon.Web.Pages.Manage;

public class QuestionsModel(ExamManagementService examService) : PageModel
{
    public Exam Exam { get; set; } = null!;

    public List<Question> Questions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var exam = await examService.GetExamAsync(id);
        if (exam is null)
        {
            return NotFound();
        }

        Exam = exam;
        Questions = await examService.GetQuestionsAsync(id);
        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        const string json = QuestionImportService.TemplateJson;
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "question-template.json");
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ImportErrors"] = "No file selected";
            return RedirectToPage(new { id });
        }

        string json;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            json = await reader.ReadToEndAsync();
        }

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        if (errors.Count > 0)
        {
            TempData["ImportErrors"] = string.Join("\n", errors);
            return RedirectToPage(new { id });
        }

        ImportSession.Set(HttpContext.Session, id, questions!);
        return RedirectToPage("/Manage/Import", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int questionId)
    {
        await examService.DeleteQuestionAsync(questionId);
        return RedirectToPage(new { id });
    }
}
