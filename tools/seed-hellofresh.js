// MongoDB seed script for Hello Fresh provider configuration
// This inserts the Hello Fresh provider configuration into MongoDB
// Usage: mongosh -u admin -p devpassword --file tools/seed-hellofresh.js

db = db.getSiblingDB('easymeals');

// Delete any existing Hello Fresh configuration
db.provider_configurations.deleteMany({ providerId: "hellofresh" });

// Insert Hello Fresh provider configuration
// Note: _id must be a string (not ObjectId) because BaseDocument uses BsonRepresentation(BsonType.String)
db.provider_configurations.insertOne({
    _id: new ObjectId().toString(),
    providerId: "hellofresh",
    enabled: true,
    discoveryStrategy: "Static", // Static HTML parsing for Hello Fresh
    recipeRootUrl: "https://www.hellofresh.com/recipes",
    recipeUrlPattern: "\\/recipe\\/[a-z0-9\\-]+\\-[a-f0-9]{24}$",
    categoryUrlPattern: "\\/recipes\\/(category|tag)\\/[a-z\\-]+",
    batchSize: 20,
    timeWindowMinutes: 30,
    minDelaySeconds: 2.0,
    maxRequestsPerMinute: 20,
    retryCount: 3,
    requestTimeoutSeconds: 30,
    createdBy: "seed-script",
    updatedBy: null,
    createdAt: new Date(),
    updatedAt: new Date(),
    version: 1
});

print("Hello Fresh provider configuration inserted successfully");

// Verify insertion
var result = db.provider_configurations.findOne({ providerId: "hellofresh" });
print("Inserted document:");
printjson(result);
