using EasyMeals.Crawler;
using EasyMeals.Crawler.Application.Services;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Infrastructure.Persistence;
using EasyMeals.Crawler.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services following Clean Architecture and DI best practices
builder.Services.AddHostedService<Worker>();

// Application Services
builder.Services.AddScoped<CrawlOrchestrationService>();

// Domain Services (Infrastructure implementations)
builder.Services.AddScoped<IRecipeRepository, InMemoryRecipeRepository>();
builder.Services.AddScoped<ICrawlStateRepository>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FileCrawlStateRepository>>();
    var stateFilePath = builder.Configuration.GetValue<string>("CrawlState:FilePath") ?? "crawl-state.json";
    return new FileCrawlStateRepository(stateFilePath, logger);
});
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
logger.LogInformation("HelloFresh Crawler starting up...");

host.Run();