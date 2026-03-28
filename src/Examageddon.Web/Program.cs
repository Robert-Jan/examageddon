using Examageddon.Data;
using Examageddon.Services;
using Examageddon.Web.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSession(opts => opts.IdleTimeout = TimeSpan.FromDays(7));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "database", "examageddon.db"))}";
builder.Services.AddDbContext<ExamageddonDbContext>(opts =>
    opts.UseSqlite(connectionString));

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddRepositories();
builder.Services.AddScoped<PersonService>();
builder.Services.AddScoped<ExamManagementService>();
builder.Services.AddScoped<ExamSessionService>();
builder.Services.AddScoped<HistoryService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExamageddonDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.MapQuestionEndpoints();
app.MapSessionEndpoints();

app.MapRazorPages();
await app.RunAsync();
