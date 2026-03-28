using Examageddon.Services.Models;
using System.Text.Json;

namespace Examageddon.Web.Pages.Manage;

internal static class ImportSession
{
    public static List<StagedQuestion>? Get(ISession session, int examId)
    {
        var json = session.GetString(Key(examId));
        return json is null ? null : JsonSerializer.Deserialize<List<StagedQuestion>>(json);
    }

    public static void Set(ISession session, int examId, List<StagedQuestion> questions)
    {
        session.SetString(Key(examId), JsonSerializer.Serialize(questions));
    }

    public static void Clear(ISession session, int examId)
    {
        session.Remove(Key(examId));
    }

    private static string Key(int examId)
    {
        return $"import_{examId}";
    }
}
