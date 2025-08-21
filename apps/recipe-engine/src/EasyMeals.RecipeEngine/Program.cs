using EasyMeals.RecipeEngine;
using EasyMeals.RecipeEngine.Domain.Configurations;
using EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure options for source provider agnostic recipe-engine
builder.Services.Configure<RecipeEngineOptions>(
	builder.Configuration.GetSection(RecipeEngineOptions.SectionName));

// Configure services following Clean Architecture and DI best practices
builder.Services.AddHostedService<Worker>();

// Application Services


// Add recipe-engine infrastructure with MongoDB using shared options pattern
builder.Services.AddRecipeEngineInfrastructure(builder.Configuration);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

IHost host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
RecipeEngineOptions RecipeEngineOptions = host.Services.GetRequiredService<IOptions<RecipeEngineOptions>>().Value;
logger.LogInformation("Recipe RecipeEngine starting up with shared MongoDB infrastructure for source provider: {SourceProvider}...",
	RecipeEngineOptions.SourceProvider);

// Ensure database is created (for development) - no longer needed with new infrastructure

host.Run();