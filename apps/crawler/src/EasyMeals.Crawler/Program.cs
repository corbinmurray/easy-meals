using System.Net;
using EasyMeals.Crawler;
using EasyMeals.Crawler.Application.Services;
using EasyMeals.Crawler.Domain.Configurations;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Infrastructure.Persistence;
using EasyMeals.Crawler.Infrastructure.Services;
using EasyMeals.Shared.Data.Extensions;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure options for source provider agnostic crawler
builder.Services.Configure<CrawlerOptions>(
    builder.Configuration.GetSection(CrawlerOptions.SectionName));

// Configure services following Clean Architecture and DI best practices
builder.Services.AddHostedService<Worker>();

// Application Services
builder.Services.AddScoped<CrawlOrchestrationService>();

// Add shared EF Core data infrastructure (using In-Memory for now, easily switchable)
builder.Services.AddEasyMealsDataInMemory();

// Domain Services (Infrastructure implementations)
// Use data repositories that bridge to the shared data infrastructure
builder.Services.AddScoped<IRecipeRepository, RecipeDataRepository>();
builder.Services.AddScoped<ICrawlStateRepository, CrawlStateDataRepository>();
builder.Services.AddScoped<IRecipeExtractor, HelloFreshRecipeExtractor>();

// HTTP Services for web scraping
builder.Services.AddHttpClient<IHelloFreshHttpService, HelloFreshHttpService>(client =>
    {
        // Set a basic user agent - the service will handle rotation
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // This is the critical part - ensure automatic decompression is enabled
        AutomaticDecompression = DecompressionMethods.All
    });

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

IHost host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
CrawlerOptions crawlerOptions = host.Services.GetRequiredService<IOptions<CrawlerOptions>>().Value;
logger.LogInformation("Recipe Crawler starting up with shared MongoDB infrastructure for source provider: {SourceProvider}...",
    crawlerOptions.SourceProvider);

// Ensure database is created (for development) - no longer needed with new infrastructure

host.Run();