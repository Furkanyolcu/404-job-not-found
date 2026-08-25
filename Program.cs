using DotNetEnv;
using _404JobNotFound.Data;
using _404JobNotFound.Endpoints;
using _404JobNotFound.Services;
using _404JobNotFound.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Serilog;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine("App_Data", "logs", "hirepilot-.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Configuration.AddEnvironmentVariables();

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "hirepilot.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddHttpClient<ILlmClient, AnthropicLlmClient>();

builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<EmailGenerationService>();
builder.Services.AddScoped<EmailSendService>();
builder.Services.AddScoped<JobIntakeService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapJobEndpoints();
app.MapApplicationEndpoints();
app.MapProfileEndpoints();
app.MapBlacklistEndpoints();
app.MapStatsEndpoints();

app.Run();
