// MongoDB Seeding Script: Ingredient Mappings
// Purpose: Seed sample ingredient normalization mappings for Recipe Engine development
// Usage: mongosh -u admin -p devpassword --file tools/seed-ingredient-mappings.js

// Switch to the easymeals database
db = db.getSiblingDB("easymeals");

print("=== Seeding Ingredient Mappings ===");

// Sample ingredient mappings for provider_001
const ingredientMappings = [
  {
    providerId: "provider_001",
    providerCode: "BROCCOLI-FROZEN-012",
    canonicalForm: "broccoli, frozen",
    notes: "Frozen broccoli florets",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "CHICKEN-BREAST-024",
    canonicalForm: "chicken breast, boneless",
    notes: "Boneless, skinless chicken breast",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "GARLIC-CLOVE-003",
    canonicalForm: "garlic cloves",
    notes: "Fresh garlic cloves",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "OLIVE-OIL-EXTRA-VIRGIN-001",
    canonicalForm: "olive oil, extra virgin",
    notes: "Extra virgin olive oil",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "SALT-SEA-FINE-005",
    canonicalForm: "salt, sea",
    notes: "Fine sea salt",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "PEPPER-BLACK-GROUND-007",
    canonicalForm: "black pepper, ground",
    notes: "Ground black pepper",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "ONION-YELLOW-MEDIUM-010",
    canonicalForm: "onion, yellow",
    notes: "Medium yellow onion",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "TOMATO-CHERRY-015",
    canonicalForm: "tomatoes, cherry",
    notes: "Cherry tomatoes",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "PASTA-PENNE-WHOLE-WHEAT-020",
    canonicalForm: "penne pasta, whole wheat",
    notes: "Whole wheat penne pasta",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "CHEESE-PARMESAN-GRATED-025",
    canonicalForm: "parmesan cheese, grated",
    notes: "Grated parmesan cheese",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "BASIL-FRESH-030",
    canonicalForm: "basil, fresh",
    notes: "Fresh basil leaves",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "LEMON-WHOLE-035",
    canonicalForm: "lemon",
    notes: "Whole lemon",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "BUTTER-UNSALTED-040",
    canonicalForm: "butter, unsalted",
    notes: "Unsalted butter",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "CREAM-HEAVY-045",
    canonicalForm: "heavy cream",
    notes: "Heavy whipping cream",
    createdAt: new Date(),
    updatedAt: null
  },
  {
    providerId: "provider_001",
    providerCode: "SPINACH-FRESH-050",
    canonicalForm: "spinach, fresh",
    notes: "Fresh baby spinach",
    createdAt: new Date(),
    updatedAt: null
  }
];

// Insert ingredient mappings (upsert to avoid duplicates)
let insertedCount = 0;
let updatedCount = 0;
let unchangedCount = 0;

ingredientMappings.forEach(mapping => {
  const result = db.ingredient_mappings.replaceOne(
    { 
      providerId: mapping.providerId,
      providerCode: mapping.providerCode
    },
    mapping,
    { upsert: true }
  );
  
  if (result.upsertedCount > 0) {
    insertedCount++;
  } else if (result.modifiedCount > 0) {
    updatedCount++;
  } else {
    unchangedCount++;
  }
});

print(`✓ Inserted: ${insertedCount} new ingredient mappings`);
print(`✓ Updated: ${updatedCount} existing ingredient mappings`);
print(`✓ Unchanged: ${unchangedCount} ingredient mappings`);

// Create indexes for ingredient_mappings collection
print("\n=== Creating Indexes for ingredient_mappings ===");

db.ingredient_mappings.createIndex(
  { providerId: 1, providerCode: 1 }, 
  { unique: true, name: "idx_provider_code" }
);
print("✓ Created unique compound index on providerId + providerCode");

db.ingredient_mappings.createIndex({ canonicalForm: 1 }, { name: "idx_canonical_form" });
print("✓ Created index on canonicalForm");

db.ingredient_mappings.createIndex({ createdAt: 1 }, { name: "idx_created_at" });
print("✓ Created index on createdAt");

// Display statistics
print("\n=== Ingredient Mappings Statistics ===");
const stats = {
  totalMappings: db.ingredient_mappings.countDocuments(),
  provider001Mappings: db.ingredient_mappings.countDocuments({ providerId: "provider_001" }),
  unmappedIngredientsNeeded: "Add more mappings as needed for your provider's ingredients"
};
printjson(stats);

// Display sample mappings
print("\n=== Sample Ingredient Mappings ===");
db.ingredient_mappings.find({ providerId: "provider_001" }).limit(5).forEach(printjson);

print("\n=== Ingredient Mappings Seeding Complete ===");
