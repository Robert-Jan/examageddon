namespace Examageddon.Data.Entities;

public class Person
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<ExamSession> Sessions { get; set; } = [];
}
