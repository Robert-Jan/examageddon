using Examageddon.Data.Entities;

namespace Examageddon.Data.Interfaces;

public interface IPersonRepository
{
    Task<List<Person>> GetAllAsync();

    Task<Person?> FindByNameAsync(string name);

    Task<Person> AddAsync(Person person);
}
