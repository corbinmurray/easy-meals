# MongoDB Seeding Scripts for Recipe Engine

This directory contains MongoDB seeding scripts for the Recipe Engine development environment.

## Overview

These scripts populate MongoDB with initial configuration and test data required for Recipe Engine development and testing. They are designed to be **idempotent** - running them multiple times will update existing data instead of creating duplicates.

## Prerequisites

- MongoDB must be running (via Docker or locally)
- Default connection: `mongodb://admin:devpassword@localhost:27017`
- Database name: `easymeals`

## Available Scripts

### 1. `seed-provider-config.js`

Seeds provider configuration for Recipe Engine testing.

**Usage:**
```bash
# From repository root
docker exec -i easy-meals-mongodb mongosh -u admin -p devpassword < tools/seed-provider-config.js
```

**What it creates:**
- Provider configuration document with ID `provider_config_001`
- Provider ID: `provider_001`
- Discovery strategy: `Dynamic` (JavaScript-rendered site crawling)
- Batch settings: 10 recipes, 10-minute time window
- Rate limiting: 2-second minimum delay, max 10 requests/minute
- Retry policy: 3 retries, 30-second timeout
- Indexes on: `providerId` (unique), `enabled`, `discoveryStrategy`, `createdAt`

**Default configuration:**
```json
{
  "providerId": "provider_001",
  "enabled": true,
  "discoveryStrategy": "Dynamic",
  "recipeRootUrl": "https://example.com/recipes",
  "batchSize": 10,
  "timeWindowMinutes": 10,
  "minDelaySeconds": 2.0,
  "maxRequestsPerMinute": 10,
  "retryCount": 3,
  "requestTimeoutSeconds": 30
}
```

**⚠️ Important:** Update `recipeRootUrl` in MongoDB before running the Recipe Engine against a real provider.

---

### 2. `seed-ingredient-mappings.js`

Seeds ingredient normalization mappings for Recipe Engine testing.

**Usage:**
```bash
# From repository root
docker exec -i easy-meals-mongodb mongosh -u admin -p devpassword < tools/seed-ingredient-mappings.js
```

**What it creates:**
- 15 sample ingredient mappings for `provider_001`
- Indexes on: `(providerId, providerCode)` (unique compound), `canonicalForm`, `createdAt`

**Sample mappings:**
| Provider Code | Canonical Form |
|---------------|----------------|
| `BROCCOLI-FROZEN-012` | `broccoli, frozen` |
| `CHICKEN-BREAST-024` | `chicken breast, boneless` |
| `GARLIC-CLOVE-003` | `garlic cloves` |
| `OLIVE-OIL-EXTRA-VIRGIN-001` | `olive oil, extra virgin` |
| `SALT-SEA-FINE-005` | `salt, sea` |
| `PEPPER-BLACK-GROUND-007` | `black pepper, ground` |
| `ONION-YELLOW-MEDIUM-010` | `onion, yellow` |
| `TOMATO-CHERRY-015` | `tomatoes, cherry` |
| `PASTA-PENNE-WHOLE-WHEAT-020` | `penne pasta, whole wheat` |
| `CHEESE-PARMESAN-GRATED-025` | `parmesan cheese, grated` |
| `BASIL-FRESH-030` | `basil, fresh` |
| `LEMON-WHOLE-035` | `lemon` |
| `BUTTER-UNSALTED-040` | `butter, unsalted` |
| `CREAM-HEAVY-045` | `heavy cream` |
| `SPINACH-FRESH-050` | `spinach, fresh` |

---

## Quick Start

**Seed all data in one command:**
```bash
# From repository root
docker exec -i easy-meals-mongodb mongosh -u admin -p devpassword < tools/seed-provider-config.js && \
docker exec -i easy-meals-mongodb mongosh -u admin -p devpassword < tools/seed-ingredient-mappings.js
```

**Verify seeded data:**
```bash
# Count provider configurations (expected: 1)
docker exec easy-meals-mongodb mongosh -u admin -p devpassword --quiet --eval "db.getSiblingDB('easymeals').provider_configurations.countDocuments()"

# Count ingredient mappings (expected: 15)
docker exec easy-meals-mongodb mongosh -u admin -p devpassword --quiet --eval "db.getSiblingDB('easymeals').ingredient_mappings.countDocuments()"

# List all collections (expected: 5)
docker exec easy-meals-mongodb mongosh -u admin -p devpassword --quiet --eval "db.getSiblingDB('easymeals').getCollectionNames()"
```

---

## Re-seeding Data

The scripts are idempotent and can be run multiple times safely. They use `replaceOne` with `upsert: true` to update existing documents instead of creating duplicates.

**Example: Update provider configuration after initial seeding:**
```bash
# Re-run the script to update the existing configuration
docker exec -i easy-meals-mongodb mongosh -u admin -p devpassword < tools/seed-provider-config.js
```

---

## Customizing Seeded Data

### Add More Ingredient Mappings

Edit `seed-ingredient-mappings.js` and add new mappings to the `ingredientMappings` array:

```javascript
{
  providerId: "provider_001",
  providerCode: "YOUR-PROVIDER-CODE",
  canonicalForm: "your canonical ingredient name",
  notes: "Optional description",
  createdAt: new Date(),
  updatedAt: null
}
```

### Add More Providers

Create a new provider configuration by duplicating the provider config in `seed-provider-config.js` and changing the `providerId`.

---

## Manual Seeding (Alternative)

If you prefer using MongoDB Compass or mongosh interactively:

1. Connect to `mongodb://admin:devpassword@localhost:27017`
2. Switch to database: `use easymeals`
3. Insert documents manually (see script files for schema)

---

## Troubleshooting

### MongoDB Connection Errors

**Error:** `MongoServerError: Authentication failed`

**Solution:**
```bash
# Verify MongoDB is running
docker ps | grep mongodb

# Check MongoDB logs
docker logs easy-meals-mongodb

# Restart MongoDB if needed
docker restart easy-meals-mongodb
```

### Script Execution Errors

**Error:** `SyntaxError: Unexpected token`

**Solution:**
Ensure you're using `mongosh` (MongoDB Shell) version 1.0+ and not the legacy `mongo` shell.

```bash
# Check mongosh version
docker exec easy-meals-mongodb mongosh --version
```

---

## Next Steps

After seeding data:

1. **Verify collections:** Check that all 5 collections exist in MongoDB
2. **Update provider URL:** Replace `https://example.com/recipes` with actual provider URL in MongoDB
3. **Run Recipe Engine:** Test the Recipe Engine with seeded configuration
4. **Add more mappings:** Add provider-specific ingredient mappings as needed

See `specs/001-hellofresh-recipe-engine/quickstart.md` for complete development setup instructions.
