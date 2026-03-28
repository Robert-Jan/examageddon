using Examageddon.Data.Entities;
using Examageddon.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Data.Repositories;

internal sealed class PersonRepository(ExamageddonDbContext db) : IPersonRepository
{
    public Task<List<Person>> GetAllAsync()
    {
        return db.Persons.OrderBy(p => p.Name).ToListAsync();
    }

    public Task<Person?> FindByNameAsync(string name)
    {
        return db.Persons.FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<Person> AddAsync(Person person)
    {
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        return person;
    }
}
