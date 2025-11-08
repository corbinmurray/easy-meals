// MongoDB Seeding Script: Provider Configuration
// Purpose: Seed initial provider configuration for Recipe Engine development
// Usage: mongosh -u admin -p devpassword --file tools/seed-provider-config.js

// Switch to the easymeals database
db = db.getSiblingDB("easymeals");

print("=== Seeding Provider Configuration ===");

// Provider configuration for testing
const providerConfig = {
  _id: "provider_config_001",
  providerId: "provider_001",
  enabled: true,
  discoveryStrategy: "Dynamic",
  recipeRootUrl: "https://example.com/recipes", // Placeholder - replace with actual provider URL
  batchSize: 10,
  timeWindowMinutes: 10,
  minDelaySeconds: 2.0,
  maxRequestsPerMinute: 10,
  retryCount: 3,
  requestTimeoutSeconds: 30,
  createdAt: new Date(),
  createdBy: "dev-setup",
  notes: "Initial provider configuration for development testing. Batch size and time window are reduced for faster testing."
};

// Insert or update the provider configuration
const result = db.provider_configurations.replaceOne(
  { providerId: "provider_001" },
  providerConfig,
  { upsert: true }
);

if (result.upsertedCount > 0) {
  print(`✓ Inserted new provider configuration: provider_001`);
} else if (result.modifiedCount > 0) {
  print(`✓ Updated existing provider configuration: provider_001`);
} else {
  print(`✓ Provider configuration already exists and unchanged: provider_001`);
}

// Create indexes for provider_configurations collection
print("\n=== Creating Indexes for provider_configurations ===");

db.provider_configurations.createIndex({ providerId: 1 }, { unique: true, name: "idx_provider_id" });
print("✓ Created unique index on providerId");

db.provider_configurations.createIndex({ enabled: 1 }, { name: "idx_enabled" });
print("✓ Created index on enabled");

db.provider_configurations.createIndex({ discoveryStrategy: 1 }, { name: "idx_discovery_strategy" });
print("✓ Created index on discoveryStrategy");

db.provider_configurations.createIndex({ createdAt: 1 }, { name: "idx_created_at" });
print("✓ Created index on createdAt");

// Display the seeded configuration
print("\n=== Seeded Provider Configuration ===");
printjson(db.provider_configurations.findOne({ providerId: "provider_001" }));

print("\n=== Provider Configuration Seeding Complete ===");
print("NOTE: Update 'recipeRootUrl' in MongoDB before running the Recipe Engine against a real provider.");
