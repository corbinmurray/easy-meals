using EasyMeals.Crawler;
using EasyMeals.Crawler.Application.Services;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Infrastructure.Persistence;
using EasyMeals.Crawler.Infrastructure.Services;
using EasyMeals.Data.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure services following Clean Architecture and DI best practices
builder.Services.AddHostedService<Worker>();

// Application Services
builder.Services.AddScoped<CrawlOrchestrationService>();

// Add shared EF Core data layer (using In-Memory for now, easily switchable)
builder.Services.AddEasyMealsDataInMemory("CrawlerDb");

// Domain Services (Infrastructure implementations)
// Use EF Core adapters that bridge to the shared data layer
builder.Services.AddScoped<IRecipeRepository, EfCoreRecipeRepositoryAdapter>();
builder.Services.AddScoped<ICrawlStateRepository, EfCoreCrawlStateRepositoryAdapter>();
builder.Services.AddScoped<IRecipeExtractor, HelloFreshRecipeExtractor>();

// HTTP Client for web requests
builder.Services.AddHttpClient<CrawlOrchestrationService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", 
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("HelloFresh Crawler starting up with shared EF Core data layer...");

// Ensure database is created (for development)
await host.Services.EnsureDatabaseCreatedAsync();

host.Run();