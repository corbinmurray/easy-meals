# Recipe Engine Testing Guide

## Overview
This document describes how to test the Recipe Engine end-to-end.

## Architecture

The Recipe Engine uses a **Sequential Saga Pattern** for processing recipes:

1. **Discovery Phase**: Recursively crawls provider websites to find recipe URLs
   - Follows category/listing pages to discover recipes
   - Returns only actual recipe URLs, not category pages
   
2. **Fingerprinting Phase**: Detects duplicates by generating fingerprints
   - Skips recipes that have already been processed
   
3. **Processing Phase**: Extracts recipe data (stubbed for now)
   - Will fetch recipe page content
   - Parse structured data
   - Normalize ingredients
   
4. **Persistence Phase**: Saves batch results to MongoDB

### Event Bus Usage

The event bus is used **ONLY** for cross-cutting concerns:
- Monitoring and alerting (`ProcessingErrorEvent`)
- Reporting and analytics (`BatchCompletedEvent`)
- Integration with downstream systems

The event bus is **NOT** used for orchestrating the workflow. The saga orchestrates itself through sequential method calls (`await` pattern).

## Running Tests

### Unit Tests
```bash
cd apps/recipe-engine
dotnet test tests/EasyMeals.RecipeEngine.Tests.Unit/
```

### Integration Tests
```bash
cd apps/recipe-engine
dotnet test tests/EasyMeals.RecipeEngine.Tests.Integration/
```

### Contract Tests
```bash
cd apps/recipe-engine
dotnet test tests/EasyMeals.RecipeEngine.Tests.Contract/
```

### All Tests
```bash
cd apps/recipe-engine
dotnet test
```

## End-to-End Testing

### Prerequisites
1. MongoDB running locally or accessible
   ```bash
   docker run -d -p 27017:27017 --name mongodb-dev \
     -e MONGO_INITDB_ROOT_USERNAME=admin \
     -e MONGO_INITDB_ROOT_PASSWORD=devpassword \
     mongo:8.0
   ```

2. Provider configuration seeded in MongoDB (in `easymeals.provider_configurations` collection)
   ```json
   {
     "ProviderId": "test-provider",
     "Enabled": true,
     "DiscoveryStrategy": "Static",
     "RecipeRootUrl": "https://example.com/recipes",
     "BatchSize": 10,
     "TimeWindowMinutes": 60,
     "MinDelaySeconds": 0.1,
     "MaxRequestsPerMinute": 60,
     "RetryCount": 3,
     "RequestTimeoutSeconds": 30
   }
   ```

### Running the Engine
```bash
cd apps/recipe-engine/src/EasyMeals.RecipeEngine
dotnet run
```

### Expected Output
The engine will:
1. Load enabled provider configurations from MongoDB
2. For each provider:
   - Execute discovery phase (crawl website for recipe URLs)
   - Execute fingerprinting phase (detect duplicates)
   - Execute processing phase (stub - marks as processed)
   - Execute persistence phase (save batch to MongoDB)
3. Log comprehensive metrics and completion status

### Monitoring Progress

Check MongoDB collections:
```javascript
// Saga state
db.saga_states.find().pretty()

// Recipe batches
db.recipe_batches.find().pretty()
```

Look for logs indicating:
- Discovery phase: "Discovered X URLs"
- Fingerprinting phase: "Fingerprinting complete: X non-duplicate URLs"
- Processing phase: "Processed: X, Failed: Y"
- Persistence phase: "Persisted RecipeBatch"
- Completion: "Saga completed successfully"

## Common Issues

### Category URLs Being Discovered Instead of Recipes
**Fixed** ✅: The discovery services now properly:
- Follow category/listing pages to find recipes within them
- Return only actual recipe URLs, not category pages themselves

### Processing Phase Interruption
**Verified** ✅: The saga executes phases sequentially with proper error handling:
- Continues processing even when individual URLs fail
- Only stops if batch size or time window limits are reached
- Handles transient and permanent errors appropriately

### Event Bus Confusion
**Clarified** ✅: The event bus is correctly used for:
- Cross-cutting concerns (monitoring, alerting)
- NOT for orchestrating the sequential workflow

## Next Steps

To enable full recipe processing:
1. Implement recipe page fetching in `ExecuteProcessingPhaseAsync`
2. Integrate recipe parser/scraper
3. Call `ProcessIngredientsAsync` for ingredient normalization
4. Create Recipe entities with extracted data
5. Persist to recipe repository

See the TODO comments in `RecipeProcessingSaga.cs` for details.
