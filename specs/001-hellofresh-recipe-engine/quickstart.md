# Quick Start: Recipe Engine Development

**Feature**: Multi-Provider Recipe Engine with Database-Driven Configuration  
**Branch**: `001-hellofresh-recipe-engine`  
**Date**: November 2, 2025

## Overview

This guide walks through setting up the local development environment for the Recipe Engine, running tests, and executing manual batch processing. Provider-specific configurations (URLs, rate limits) are stored in MongoDB for security and never committed to GitHub.

---

## Prerequisites

### Required Software

- **.NET 8 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Docker Desktop**: [Download](https://www.docker.com/products/docker-desktop) (for MongoDB and Playwright)
- **MongoDB Compass** (optional): [Download](https://www.mongodb.com/products/compass) (for database inspection)
- **Visual Studio Code** or **JetBrains Rider** (IDE)

### Verify Installation

```powershell
# Check .NET version (should be 8.0.x)
dotnet --version

# Check Docker version
docker --version

# Check Docker is running
docker ps
```

---

## Repository Setup

### 1. Clone Repository

```powershell
git clone https://github.com/your-org/easy-meals.git
cd easy-meals
```

### 2. Checkout Feature Branch

```powershell
git checkout 001-hellofresh-recipe-engine
```

### 3. Restore Dependencies

```powershell
# Navigate to Recipe Engine solution
cd apps/recipe-engine

# Restore NuGet packages
dotnet restore EasyMeals.RecipeEngine.sln
```

---

## Local Infrastructure Setup

### MongoDB via Docker

The Recipe Engine requires MongoDB for persistence (recipes, saga state, fingerprints, ingredient mappings).

#### Start MongoDB Container

```powershell
# Start MongoDB with authentication
docker run -d `
  --name easy-meals-mongodb `
  -p 27017:27017 `
  -e MONGO_INITDB_ROOT_USERNAME=admin `
  -e MONGO_INITDB_ROOT_PASSWORD=devpassword `
  -v easy-meals-mongodb-data:/data/db `
  mongo:7.0

# Verify MongoDB is running
docker ps | Select-String "easy-meals-mongodb"
```

#### Connection String

```
mongodb://admin:devpassword@localhost:27017
```

#### MongoDB Compass (Optional)

1. Open MongoDB Compass
2. Connect to `mongodb://admin:devpassword@localhost:27017`
3. Create database: `easy-meals-recipes`
4. Collections will be auto-created on first run

---

## Configuration

### 1. User Secrets (Development)

Store connection strings and secrets using .NET User Secrets (not committed to git).

```powershell
# Navigate to main project
cd src/EasyMeals.RecipeEngine

# Initialize user secrets
dotnet user-secrets init

# Set MongoDB connection string
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://admin:devpassword@localhost:27017"
dotnet user-secrets set "MongoDB:DatabaseName" "easy-meals-recipes"
```

### 2. Database-Driven Provider Configuration

**IMPORTANT**: Provider-specific settings (URLs, rate limits) are stored in MongoDB, **NOT** in appsettings.json.

#### Why Database-Driven Configuration?

- **Security**: Provider URLs and TOS-sensitive data never committed to GitHub
- **Dynamic Updates**: Change settings without redeployment
- **Audit Trail**: Track who changed what configuration and when
- **Environment Isolation**: Different configs for dev/staging/prod via database

#### Seed Provider Configuration

Use MongoDB Compass, mongosh, or a CLI tool to insert provider configurations:

**Example: MongoDB Compass**

1. Connect to `mongodb://admin:devpassword@localhost:27017`
2. Navigate to `easy-meals-recipes` database
3. Create `provider_configurations` collection
4. Insert document:

```json
{
  "_id": "provider_config_001",
  "providerId": "provider_001",
  "enabled": true,
  "discoveryStrategy": "Dynamic",
  "recipeRootUrl": "{your_provider_url_here}",
  "batchSize": 10,
  "timeWindowMinutes": 10,
  "minDelaySeconds": 2,
  "maxRequestsPerMinute": 10,
  "retryCount": 3,
  "requestTimeoutSeconds": 30,
  "createdAt": "2025-11-02T00:00:00Z",
  "createdBy": "dev-setup"
}
```

**Example: mongosh (MongoDB Shell)**

```javascript
db = db.getSiblingDB("easy-meals-recipes");

db.provider_configurations.insertOne({
  providerId: "provider_001",
  enabled: true,
  discoveryStrategy: "Dynamic",
  recipeRootUrl: "{your_provider_url_here}", // Replace with actual URL
  batchSize: 10,
  timeWindowMinutes: 10,
  minDelaySeconds: 2.0,
  maxRequestsPerMinute: 10,
  retryCount: 3,
  requestTimeoutSeconds: 30,
  createdAt: new Date(),
  createdBy: "dev-setup",
});
```

**Note**: For development, use smaller `batchSize` (10) and shorter `timeWindowMinutes` (10) to speed up testing.

### 3. appsettings.Development.json (Minimal)

Only logging and non-sensitive settings:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "EasyMeals.RecipeEngine": "Debug"
    }
  }
}
```

---

## Building the Solution

### Build All Projects

```powershell
# From apps/recipe-engine directory
dotnet build EasyMeals.RecipeEngine.sln
```

Expected output:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Build Individual Project

```powershell
# Build Domain layer
dotnet build src/EasyMeals.RecipeEngine.Domain/EasyMeals.RecipeEngine.Domain.csproj

# Build Application layer
dotnet build src/EasyMeals.RecipeEngine.Application/EasyMeals.RecipeEngine.Application.csproj

# Build Infrastructure layer
dotnet build src/EasyMeals.RecipeEngine.Infrastructure/EasyMeals.RecipeEngine.Infrastructure.csproj

# Build entry point
dotnet build src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj
```

---

## Running Tests

### Run All Tests

```powershell
# From apps/recipe-engine directory
dotnet test EasyMeals.RecipeEngine.sln
```

### Run Specific Test Project

```powershell
# Contract tests (saga state transitions)
dotnet test tests/EasyMeals.RecipeEngine.Tests.Contract/EasyMeals.RecipeEngine.Tests.Contract.csproj

# Integration tests (end-to-end workflow)
dotnet test tests/EasyMeals.RecipeEngine.Tests.Integration/EasyMeals.RecipeEngine.Tests.Integration.csproj

# Unit tests (business logic)
dotnet test tests/EasyMeals.RecipeEngine.Tests.Unit/EasyMeals.RecipeEngine.Tests.Unit.csproj
```

### Run Single Test

```powershell
dotnet test --filter "FullyQualifiedName=EasyMeals.RecipeEngine.Tests.Unit.Normalization.IngredientNormalizationServiceTests.NormalizeAsync_WhenMappingExists_ReturnsCanonicalForm"
```

### Test Coverage Report

```powershell
# Install ReportGenerator tool (one-time)
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html

# Open report in browser
Start-Process coverage-report/index.html
```

---

## Running the Application

### Console Application (Manual Batch Processing)

The Recipe Engine is a console application designed for scheduled batch processing (Coolify handles scheduling in production).

#### Run Locally

```powershell
# From apps/recipe-engine/src/EasyMeals.RecipeEngine directory
dotnet run

# Or with release configuration
dotnet run --configuration Release
```

Expected output:

```
[14:32:15 INF] Recipe Engine starting...
[14:32:16 INF] Loading provider configurations from MongoDB...
[14:32:16 INF] Provider: provider_001, Enabled: true, Discovery: Dynamic
[14:32:16 INF] Starting batch processing for provider_001
[14:32:16 INF] Batch ID: abc123, Size: 10, Time Window: 00:10:00
[14:32:17 INF] Discovery phase: Found 25 recipe URLs
[14:32:18 INF] Fingerprinting phase: 15 new recipes (10 duplicates skipped)
[14:32:20 INF] Processing phase: 10/15 recipes processed (5 remaining for next batch)
[14:32:21 INF] Persistence phase: 10 recipes saved to MongoDB
[14:32:21 INF] Batch completed: Processed=10, Skipped=10, Failed=0, Duration=00:00:05
[14:32:21 INF] Recipe Engine stopped
```

#### Manual Provider Selection

```powershell
# Override provider ID via command-line argument (if implemented)
dotnet run -- --provider=provider_001

# Override batch size and time window
dotnet run -- --provider=provider_001 --batch-size=5 --time-window=5
```

---

## Debugging

### Visual Studio Code

1. Open `apps/recipe-engine` folder in VS Code
2. Press `F5` or select **Run > Start Debugging**
3. Set breakpoints in saga, services, or handlers
4. View logs in Debug Console

#### `.vscode/launch.json` Configuration

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (console)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/EasyMeals.RecipeEngine/bin/Debug/net8.0/EasyMeals.RecipeEngine.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/EasyMeals.RecipeEngine",
      "console": "internalConsole",
      "stopAtEntry": false
    }
  ]
}
```

### JetBrains Rider

1. Open `EasyMeals.RecipeEngine.sln` in Rider
2. Set `EasyMeals.RecipeEngine` as startup project
3. Press `F5` or click **Run > Debug**
4. Set breakpoints and inspect variables

---

## Database Inspection

### MongoDB Compass

1. Connect to `mongodb://admin:devpassword@localhost:27017`
2. Navigate to `easy-meals-recipes` database
3. View collections:
   - `provider_configurations` - Provider settings (URLs, rate limits, discovery strategy)
   - `recipes` - Processed recipes
   - `recipe_batches` - Batch processing history
   - `recipe_fingerprints` - Duplicate detection
   - `ingredient_mappings` - Normalization mappings
   - `recipe_processing_saga_states` - Saga orchestration state

### MongoDB Shell

```powershell
# Connect to MongoDB container
docker exec -it easy-meals-mongodb mongosh -u admin -p devpassword

# Switch to recipes database
use easy-meals-recipes

# Query recipes
db.recipes.find({ ProviderId: "provider_001" }).limit(5)

# Query recent batches
db.recipe_batches.find().sort({ StartedAt: -1 }).limit(5)

# Count fingerprints
db.recipe_fingerprints.countDocuments({ ProviderId: "provider_001" })

# Find unmapped ingredients (null CanonicalForm)
db.recipes.aggregate([
  { $unwind: "$Ingredients" },
  { $match: { "Ingredients.CanonicalForm": null } },
  { $group: { _id: "$Ingredients.ProviderCode", count: { $sum: 1 } } },
  { $sort: { count: -1 } }
])
```

---

## Seeding Test Data

### Ingredient Mappings

Seed ingredient normalization mappings for testing:

```powershell
# From apps/recipe-engine directory
dotnet run --project tools/DataSeeder -- --seed-ingredients
```

**Manual Seeding (MongoDB Shell)**:

```javascript
db.ingredient_mappings.insertMany([
  {
    providerId: "provider_001",
    providerCode: "BROCCOLI-FROZEN-012",
    canonicalForm: "broccoli, frozen",
    createdAt: new Date(),
    updatedAt: null,
  },
  {
    providerId: "provider_001",
    providerCode: "CHICKEN-BREAST-024",
    canonicalForm: "chicken breast, boneless",
    createdAt: new Date(),
    updatedAt: null,
  },
  // ... add more mappings as needed
]);
```

---

## Troubleshooting

### MongoDB Connection Errors

**Error**: `MongoConnectionException: Unable to reach primary`

**Solution**:

```powershell
# Restart MongoDB container
docker restart easy-meals-mongodb

# Verify container is running
docker logs easy-meals-mongodb

# Check connection string in user secrets
dotnet user-secrets list
```

---

### Playwright Browser Not Found (Dynamic Crawling)

**Error**: `BrowserNotFoundException: Chromium browser not found`

**Solution**:

```powershell
# Install Playwright browsers (one-time setup)
pwsh tools/playwright.ps1 install chromium

# Or manually
npx playwright install chromium
```

**Docker**: Playwright browsers are pre-installed in the official Docker image (`mcr.microsoft.com/playwright/dotnet:v1.40.0-focal`).

---

### Rate Limiting Issues

**Error**: `RateLimitExceededException: No tokens available`

**Solution**:

- Update provider configuration in MongoDB to increase `MaxRequestsPerMinute`
- Reduce `BatchSize` in provider configuration to process fewer recipes per run
- Increase `TimeWindowMinutes` in provider configuration to allow more time per batch

**Example MongoDB update**:

```javascript
db.provider_configurations.updateOne(
  { providerId: "provider_001" },
  { $set: { maxRequestsPerMinute: 15, batchSize: 50 } }
);
```

---

### Unmapped Ingredients

**Warning**: `Unmapped ingredient: Provider=provider_001, Code=UNKNOWN-ITEM-999`

**Solution**:

1. View unmapped ingredients in MongoDB:
   ```javascript
   db.recipes.aggregate([
     { $unwind: "$Ingredients" },
     { $match: { "Ingredients.CanonicalForm": null } },
     { $group: { _id: "$Ingredients.ProviderCode", count: { $sum: 1 } } },
   ]);
   ```
2. Add mappings to `ingredient_mappings` collection
3. Reprocess recipes (future feature: manual reprocessing tool)

---

## Next Steps

1. **Implement Saga**: Complete `RecipeProcessingSaga` with all state transitions
2. **Implement Discovery Services**: Create `StaticCrawlDiscoveryService`, `DynamicCrawlDiscoveryService`, `ApiDiscoveryService`
3. **Implement Normalization**: Create `IngredientNormalizationService` with MongoDB lookups
4. **Implement Fingerprinting**: Create `RecipeFingerprintService` with SHA256 hashing
5. **Implement Rate Limiting**: Create `TokenBucketRateLimiter` with background refill task
6. **Write Tests**: Contract tests for saga, integration tests for workflow, unit tests for services

Refer to `tasks.md` (generated via `/speckit.tasks` command) for detailed implementation checklist.
