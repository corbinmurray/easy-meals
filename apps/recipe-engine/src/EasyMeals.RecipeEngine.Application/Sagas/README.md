# Recipe Processing Saga Implementation

## Overview

The `RecipeProcessingSaga` demonstrates the **Saga Pattern** for managing complex, multi-step business processes in our recipe engine. This pattern is essential for maintaining consistency in distributed systems and handling long-running workflows with proper error recovery.

## What is the Saga Pattern?

The Saga Pattern is a microservices architecture pattern that manages data consistency across services in a distributed transaction. Instead of using traditional ACID transactions, sagas coordinate multiple business transactions using a sequence of compensating actions.

### Key Characteristics

1. **Choreographed Steps**: Each step in the saga is a local transaction
2. **Compensating Transactions**: If a step fails, previous steps are "undone" via compensating actions
3. **Event-Driven**: Uses domain events to coordinate between steps
4. **Eventually Consistent**: Maintains eventual consistency rather than immediate consistency

## Recipe Processing Workflow

Our saga orchestrates the complete recipe processing pipeline:

### Phase 1: Discovery

- **Purpose**: Discover recipe URLs from configured provider sites
- **Input**: Provider configurations (AllRecipes, FoodNetwork, etc.)
- **Output**: Collection of `DiscoveredUrl` objects
- **Compensation**: None needed (read-only operation)

### Phase 2: Fingerprinting

- **Purpose**: Scrape web content and create fingerprints for change detection
- **Input**: Discovered URLs
- **Output**: Collection of `Fingerprint` entities
- **Events Published**: `FingerprintCreatedEvent`, `ScrapingFailedEvent`
- **Compensation**: Delete created fingerprints

### Phase 3: Processing

- **Purpose**: Extract structured recipe data from fingerprints
- **Input**: High-quality fingerprints
- **Output**: Collection of `Recipe` entities
- **Events Published**: `RecipeCreatedEvent`, `FingerprintProcessedEvent`
- **Compensation**: Delete extracted recipes

### Phase 4: Persistence

- **Purpose**: Save all entities to their respective repositories
- **Input**: Fingerprints and extracted recipes
- **Output**: Persisted data
- **Compensation**: Delete all saved entities

### Phase 5: Completion

- **Purpose**: Clean up saga state and publish completion events
- **Input**: Processing results
- **Output**: Completion metrics and events

## Event-Driven Architecture

The saga publishes domain events at each step:

```csharp
// Discovery events
RecipeUrlsDiscoveredEvent
DiscoveryStartedEvent
DiscoveryCompletedEvent
DiscoveryFailedEvent

// Fingerprinting events
FingerprintCreatedEvent
ScrapingFailedEvent
ContentChangedEvent

// Recipe processing events
RecipeCreatedEvent
FingerprintProcessedEvent
FingerprintRetryEvent
```

## Error Handling & Compensation

### Compensating Transactions

When a saga step fails, compensating transactions "undo" the effects of previous successful steps:

```csharp
private async Task ExecuteCompensatingTransactionsAsync(CancellationToken cancellationToken)
{
	// Compensate Recipe creation
	if (_sagaState.TryGetValue("Recipes", out var recipesObj) && recipesObj is List<Recipe> recipes)
	{
		foreach (var recipe in recipes)
		{
			await _recipeRepository.DeleteAsync(recipe.Id, cancellationToken);
		}
	}

	// Compensate Fingerprint creation
	if (_sagaState.TryGetValue("Fingerprints", out var fingerprintsObj) && fingerprintsObj is List<Fingerprint> fingerprints)
	{
		foreach (var fingerprint in fingerprints)
		{
			await _fingerprintRepository.DeleteAsync(fingerprint.Id, cancellationToken);
		}
	}
}
```

This saga implementation demonstrates enterprise-level patterns for handling complex, distributed workflows while maintaining consistency, observability, and fault tolerance.
