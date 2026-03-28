using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;

namespace Examageddon.Services;

public class PersonService(IPersonRepository personRepository)
{
    public Task<List<Person>> GetAllAsync()
    {
        return personRepository.GetAllAsync();
    }

    public async Task<Person> GetOrCreateAsync(string name)
    {
        var existing = await personRepository.FindByNameAsync(name);
        return existing ?? await personRepository.AddAsync(new Person { Name = name });
    }
}
