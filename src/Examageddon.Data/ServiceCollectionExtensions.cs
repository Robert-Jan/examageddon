using Examageddon.Data.Interfaces;
using Examageddon.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Examageddon.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
        services.AddScoped<IHistoryRepository, HistoryRepository>();
        return services;
    }
}
