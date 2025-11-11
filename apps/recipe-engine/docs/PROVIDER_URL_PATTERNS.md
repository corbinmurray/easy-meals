# Provider URL Pattern Configuration

This document explains how to configure provider-specific regex patterns for recipe and category URL discovery.

## Overview

The Recipe Engine now supports provider-specific regex patterns to identify:
- **Recipe URLs**: URLs that point to actual recipe pages
- **Category URLs**: URLs that point to category/listing pages (which should be crawled to find recipes)

When these patterns are configured, they take precedence over the default pattern matching logic. If no patterns are configured, the service falls back to default behavior.

## Configuration Fields

Add these optional fields to your provider configuration in MongoDB:

```json
{
  "providerId": "hellofresh",
  "enabled": true,
  "discoveryStrategy": "Static",
  "recipeRootUrl": "https://www.hellofresh.com",
  "batchSize": 50,
  "timeWindowMinutes": 60,
  "minDelaySeconds": 1.0,
  "maxRequestsPerMinute": 60,
  "retryCount": 3,
  "requestTimeoutSeconds": 30,
  
  // Optional: Regex pattern to identify recipe URLs
  "recipeUrlPattern": "\\/recipe\\/[a-z0-9\\-]+\\-[a-f0-9]{24}$",
  
  // Optional: Regex pattern to identify category/listing URLs
  "categoryUrlPattern": "\\/recipes\\/(category|tag)\\/[a-z\\-]+"
}
```

## HelloFresh Example

HelloFresh uses a specific URL structure:
- Recipe URLs: `https://www.hellofresh.com/recipe/chicken-pasta-507f1f77bcf86cd799439011`
- Category URLs: `https://www.hellofresh.com/recipes/category/dinner`

### Recipe URL Pattern

The recipe URL pattern identifies pages with a recipe ID (24-character hex string):

```regex
\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$
```

**Pattern Breakdown:**
- `\/recipe\/` - Matches the literal `/recipe/` path
- `[a-z0-9\-]+` - Matches the recipe slug (lowercase letters, numbers, hyphens)
- `\-` - Matches a hyphen separator
- `[a-f0-9]{24}` - Matches exactly 24 hexadecimal characters (MongoDB ObjectId format)
- `$` - Ensures the pattern ends here (no additional path segments)

**Matches:**
- ✅ `/recipe/chicken-pasta-507f1f77bcf86cd799439011`
- ✅ `/recipe/beef-stew-507f191e810c19729de860ea`
- ❌ `/recipe/chicken-pasta` (missing ID)
- ❌ `/recipe/category/dinner` (doesn't match pattern)

### Category URL Pattern

The category URL pattern identifies listing pages that should be crawled:

```regex
\/recipes\/(category|tag)\/[a-z\-]+
```

**Pattern Breakdown:**
- `\/recipes\/` - Matches the literal `/recipes/` path
- `(category|tag)` - Matches either "category" or "tag"
- `\/` - Matches a forward slash
- `[a-z\-]+` - Matches lowercase letters and hyphens (category/tag name)

**Matches:**
- ✅ `/recipes/category/dinner`
- ✅ `/recipes/tag/vegetarian`
- ✅ `/recipes/category/quick-meals`
- ❌ `/recipe/chicken-pasta-507f1f77bcf86cd799439011` (recipe, not category)
- ❌ `/about` (not a category page)

## Default Patterns (Fallback)

If no patterns are configured, the service uses these default substring checks:

### Default Recipe Patterns (any of these substrings):
- `/recipe/`
- `/recipes/`
- `/food/recipe`
- `/cooking/recipe`
- `/r/`
- `/dish/`

### Default Category Patterns (any of these substrings):
- `/category`
- `/categories`
- `/tag`
- `/tags`
- `/collection`
- `/cuisine`
- `/meal-type`
- `/recipes` (main listings page)

### Excluded Patterns (always excluded):
- `/about`
- `/contact`
- `/privacy`
- `/terms`
- `/login`
- `/signup`
- `/cart`
- `/checkout`
- `/account`
- `/search`

## Regex Guidelines

When creating regex patterns:

1. **Test your patterns** - Use a regex tester to validate against sample URLs
2. **Use timeout-safe patterns** - Avoid catastrophic backtracking patterns
3. **Be specific** - More specific patterns reduce false positives
4. **Use case-insensitive matching** - Patterns are automatically case-insensitive
5. **Escape special characters** - Remember to escape `/`, `.`, `?`, etc.
6. **Consider variations** - Account for different URL structures the provider might use

## Pattern Validation

The system validates regex patterns when loading provider configurations:

- ✅ Valid patterns are compiled with a 1-second timeout
- ❌ Invalid regex syntax throws `ArgumentException`
- ⏱️ Patterns that timeout during matching fall back to default patterns
- 📝 Warnings are logged for pattern failures

## MongoDB Update Query

To add patterns to an existing provider configuration:

```javascript
db.provider_configurations.updateOne(
  { providerId: "hellofresh" },
  {
    $set: {
      recipeUrlPattern: "\\/recipe\\/[a-z0-9\\-]+\\-[a-f0-9]{24}$",
      categoryUrlPattern: "\\/recipes\\/(category|tag)\\/[a-z\\-]+",
      updatedAt: new Date(),
      updatedBy: "admin"
    }
  }
);
```

## Benefits

Provider-specific regex patterns enable:

1. **Higher Accuracy** - Precise identification of recipe vs category URLs
2. **Better Discovery** - Reduced false positives and negatives
3. **Provider Flexibility** - Each provider can have unique URL structures
4. **Backward Compatibility** - Fallback to default patterns when not configured
5. **Performance** - Compiled regex patterns are cached for efficiency
