using Examageddon.Services;

namespace Examageddon.Web.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions/{id:int}/complete", async (int id, ExamSessionService svc) =>
        {
            await svc.CompleteSessionAsync(id);
            return Results.Redirect($"/sessions/{id}/result");
        });
    }
}
