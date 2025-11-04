# Easy Meals RecipeEngine

A source provider agnostic recipe recipe-engine built with Clean Architecture principles and MongoDB persistence.

## Overview

This recipe-engine is designed to extract recipe data from various meal kit and recipe providers, storing the results in a MongoDB database through the shared data infrastructure. The recipe-engine follows Domain-Driven Design (DDD) patterns and supports multiple source providers through configuration.

## Features

- **Source Provider Agnostic**: Easily configurable to work with different recipe providers
- **MongoDB Persistence**: Native MongoDB document storage with embedded documents and arrays
- **Clean Architecture**: Clear separation between Domain, Application, and Infrastructure layers
- **Resumable Crawling**: Maintains crawl state for fault tolerance and resumability
- **Bulk Operations**: Efficient batch processing for high-performance crawling
- **Configuration-Driven**: Provider settings managed through appsettings.json

## Supported Providers

- **Extensible**: Easy to add support for additional providers

## Configuration

### Basic Configuration

Update your `appsettings.json` to specify the source provider:

```json
{
  "RecipeEngine": {
    "SourceProvider": "SourceProvider",
    "DefaultPriority": 1,
    "DelayBetweenRequestsSeconds": 2,
    "RequestTimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

### Adding New Providers

To add support for a new provider (e.g., "BlueApron"):

1. **Update Configuration**:

   ```json
   {
     "RecipeEngine": {
       "SourceProvider": "BlueApron",
       "DefaultPriority": 2,
       "DelayBetweenRequestsSeconds": 3,
       "RequestTimeoutSeconds": 45,
       "MaxRetries": 2
     }
   }
   ```

2. **Create Provider-Specific Extractor**:

   ```csharp
   public class BlueApronRecipeExtractor : IRecipeExtractor
   {
       // Implement provider-specific extraction logic
   }
   ```

3. **Register in DI Container**:

   ```csharp
   // In Program.cs
   builder.Services.AddScoped<IRecipeExtractor, BlueApronRecipeExtractor>();
   ```

4. **Update HTTP Service** (if needed):
   ```csharp
   public class BlueApronHttpService : IBlueApronHttpService
   {
       // Provider-specific HTTP handling
   }
   ```

## Architecture

### Domain Layer

- **Entities**: `Recipe` - Core recipe entity
- **Value Objects**: `CrawlState` - Immutable state representation
- **Interfaces**: Repository and service contracts
- **Configurations**: `RecipeEngineOptions` - Source provider configuration

### Application Layer

- **Services**: `CrawlOrchestrationService` - Coordinates crawling operations
- **Use Cases**: Business logic for crawling workflows

### Infrastructure Layer

- **Persistence**: Source provider agnostic repositories
  - `CrawlStateDataRepository` - Manages crawl state persistence
  - `RecipeDataRepository` - Handles recipe data storage
- **Services**: Provider-specific extractors and HTTP services
- **External**: Web scraping and data extraction services

## Data Flow

1. **Configuration Loading**: RecipeEngine options loaded from appsettings.json
2. **State Restoration**: Previous crawl state loaded from MongoDB
3. **URL Processing**: Pending URLs processed according to provider logic
4. **Data Extraction**: Provider-specific extractors parse recipe data
5. **Persistence**: Recipes and state saved to MongoDB with source provider tagging
6. **State Management**: Crawl progress maintained for resumability

## MongoDB Integration

The recipe-engine leverages MongoDB's document-oriented features:

- **Native Arrays**: Ingredient and instruction lists stored as MongoDB arrays
- **Embedded Documents**: Nutritional information stored as embedded documents
- **Source Provider Tagging**: All data tagged with source provider for multi-provider support
- **Indexing**: Optimized indexes for efficient querying and crawl state management

## Monitoring

- **Comprehensive Logging**: Structured logging with source provider context
- **Success/Failure Tracking**: Detailed metrics for crawl operations
- **State Persistence**: Real-time state updates for monitoring crawl progress

## Extension Points

The recipe-engine is designed for easy extensibility:

1. **New Source Providers**: Add new extractors and configure through settings
2. **Custom Data Fields**: Extend recipe entities with provider-specific fields
3. **Processing Logic**: Customize crawl orchestration for different provider requirements
4. **Storage Options**: Alternative persistence strategies through repository pattern

## Best Practices

- **Rate Limiting**: Respect provider rate limits through configuration
- **Error Handling**: Graceful degradation with retry mechanisms
- **Resource Management**: Efficient memory usage for large-scale crawling
- **Data Integrity**: Duplicate detection and consistency validation

## Getting Started

1. Clone the repository
2. Update `appsettings.json` with your preferred source provider
3. Configure MongoDB connection in the shared data infrastructure
4. Run the recipe-engine: `dotnet run`

The recipe-engine will automatically begin processing based on your configuration and maintain state for subsequent runs.
