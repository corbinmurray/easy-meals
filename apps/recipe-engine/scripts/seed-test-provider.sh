#!/bin/bash

# Script to seed a test provider configuration in MongoDB
# This enables end-to-end testing of the Recipe Engine

echo "Seeding test provider configuration..."

# MongoDB connection details
MONGO_HOST="${MONGO_HOST:-localhost}"
MONGO_PORT="${MONGO_PORT:-27017}"
MONGO_USER="${MONGO_USER:-admin}"
MONGO_PASS="${MONGO_PASS:-devpassword}"
DATABASE="easymeals"
COLLECTION="provider_configurations"

# Provider configuration
PROVIDER_CONFIG='{
  "ProviderId": "test-provider",
  "Enabled": true,
  "DiscoveryStrategy": "Static",
  "RecipeRootUrl": "https://www.allrecipes.com/recipes",
  "BatchSize": 10,
  "TimeWindowMinutes": 5,
  "MinDelaySeconds": 0.5,
  "MaxRequestsPerMinute": 10,
  "RetryCount": 3,
  "RequestTimeoutSeconds": 30,
  "CreatedAt": "'"$(date -u +%Y-%m-%dT%H:%M:%SZ)"'",
  "UpdatedAt": "'"$(date -u +%Y-%m-%dT%H:%M:%SZ)"'"
}'

# Insert the provider configuration
mongosh "mongodb://${MONGO_USER}:${MONGO_PASS}@${MONGO_HOST}:${MONGO_PORT}/${DATABASE}" --eval "
db.${COLLECTION}.insertOne(${PROVIDER_CONFIG})
print('✅ Test provider configuration seeded successfully')
print('Provider ID: test-provider')
print('Recipe Root URL: https://www.allrecipes.com/recipes')
print('Batch Size: 10')
print('Time Window: 5 minutes')
"

echo ""
echo "You can now run the Recipe Engine:"
echo "  cd apps/recipe-engine/src/EasyMeals.RecipeEngine"
echo "  dotnet run"
