using Anthropic.SDK;
using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WorkforceDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=workforce.db"));
builder.Services.AddScoped<DataService>();
builder.Services.AddSingleton(_ =>
    new AnthropicClient(builder.Configuration["ANTHROPIC_API_KEY"]!));
builder.Services.AddSingleton<IClaudeClient, AnthropicClaudeClient>();
builder.Services.AddScoped<AgentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

    if (!app.Environment.IsEnvironment("Testing") && !await db.Resources.AnyAsync())
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "data", "resources.json"));
        var records = System.Text.Json.JsonSerializer.Deserialize<List<ResourceSeed>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower })!;
        db.Resources.AddRange(records.Select(r => new Resource
        {
            ResourceId = r.ResourceId, Name = r.Name, ResourceType = r.ResourceType,
            Company = r.Company, Seniority = r.Seniority, RoleType = r.RoleType,
            Division = r.Division, Country = r.Country, MonthlyRate = r.MonthlyRate,
            Jan = r.Jan, Feb = r.Feb, Mar = r.Mar, Apr = r.Apr, May = r.May,
            Jun = r.Jun, Jul = r.Jul, Aug = r.Aug, Sep = r.Sep, Oct = r.Oct,
            Nov = r.Nov, Dec = r.Dec
        }));
        await db.SaveChangesAsync();
    }

    if (!app.Environment.IsEnvironment("Testing"))
    {
        await db.Mappings.ExecuteDeleteAsync();
        var lookup = await db.Resources.ToDictionaryAsync(r => r.ResourceId, r => r.Id);
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "data", "mappings.json"));
        var records = System.Text.Json.JsonSerializer.Deserialize<List<MappingSeed>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower })!;
        db.Mappings.AddRange(records.Select(m => new Mapping
        {
            ResourceId = lookup[m.ResourceId],
            ActivityId = m.ActivityId,
            Percent = m.Percent,
            Month = m.Month
        }));
        await db.SaveChangesAsync();
    }
}

app.UseDefaultFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();

public partial class Program { }
