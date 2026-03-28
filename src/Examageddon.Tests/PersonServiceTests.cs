using Examageddon.Data.Entities;
using Examageddon.Data.Repositories;
using Examageddon.Services;
using Examageddon.Tests.Helpers;

namespace Examageddon.Tests;

public class PersonServiceTests
{
    [Fact]
    public async Task GetAllAsyncReturnsAllPersons()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Persons.AddRange(
            new Person { Name = "Alice" },
            new Person { Name = "Bob" });
        await ctx.SaveChangesAsync();

        var svc = new PersonService(new PersonRepository(ctx));
        var result = await svc.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetOrCreateAsyncCreatesNewPerson()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = new PersonService(new PersonRepository(ctx));

        var person = await svc.GetOrCreateAsync("Charlie");

        Assert.NotEqual(0, person.Id);
        Assert.Equal("Charlie", person.Name);
    }

    [Fact]
    public async Task GetOrCreateAsyncReturnsExistingPerson()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Persons.Add(new Person { Name = "Dana" });
        await ctx.SaveChangesAsync();

        var svc = new PersonService(new PersonRepository(ctx));
        var person1 = await svc.GetOrCreateAsync("Dana");
        var person2 = await svc.GetOrCreateAsync("Dana");

        Assert.Equal(person1.Id, person2.Id);
        Assert.Equal(1, ctx.Persons.Count(p => p.Name == "Dana"));
    }
}
