using Examageddon.Services;

namespace Examageddon.Web.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
        app.MapGet("/images/question/{id:int}", async (int id, ExamManagementService svc) =>
        {
            var (data, contentType) = await svc.GetImageAsync(id);
            if (data is null)
            {
                return Results.NotFound();
            }

            return Results.File(data, contentType ?? "application/octet-stream");
        });
    }
}
