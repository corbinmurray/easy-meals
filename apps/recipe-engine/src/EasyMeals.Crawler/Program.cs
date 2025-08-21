using EasyMeals.Crawler;
using EasyMeals.Crawler.Domain.Configurations;
using EasyMeals.Crawler.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure options for source provider agnostic crawler
builder.Services.Configure<CrawlerOptions>(
	builder.Configuration.GetSection(CrawlerOptions.SectionName));

// Configure services following Clean Architecture and DI best practices
builder.Services.AddHostedService<Worker>();

// Application Services


// Add crawler infrastructure with MongoDB using shared options pattern
builder.Services.AddCrawlerInfrastructure(builder.Configuration);

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