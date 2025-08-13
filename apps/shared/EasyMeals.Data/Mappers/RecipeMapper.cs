using System.Text.Json;
using EasyMeals.Data.Entities;

namespace EasyMeals.Data.Mappers;

/// <summary>
/// Mapping utilities for converting between domain objects and data entities
/// This allows keeping domain and data layers separate while enabling easy conversion
/// </summary>
public static class RecipeMapper
{
    /// <summary>
    /// Maps a domain Recipe to a RecipeEntity for persistence
    /// Note: This assumes the domain Recipe has the same basic structure
    /// You'll need to adjust this based on your actual domain model
    /// </summary>
    public static RecipeEntity ToEntity<TDomainRecipe>(TDomainRecipe domainRecipe)
        where TDomainRecipe : class
    {
        // Using reflection to get properties - this is a simple approach
        // For better performance, consider using AutoMapper or manual mapping
        var type = typeof(TDomainRecipe);
        
        var entity = new RecipeEntity();
        
        // Map basic properties if they exist
        if (type.GetProperty("Id") is not null)
            entity.Id = GetPropertyValue<string>(domainRecipe, "Id") ?? string.Empty;
            
        if (type.GetProperty("Title") is not null)
            entity.Title = GetPropertyValue<string>(domainRecipe, "Title") ?? string.Empty;
            
        if (type.GetProperty("Description") is not null)
            entity.Description = GetPropertyValue<string>(domainRecipe, "Description") ?? string.Empty;
            
        if (type.GetProperty("ImageUrl") is not null)
            entity.ImageUrl = GetPropertyValue<string>(domainRecipe, "ImageUrl") ?? string.Empty;
            
        if (type.GetProperty("SourceUrl") is not null)
            entity.SourceUrl = GetPropertyValue<string>(domainRecipe, "SourceUrl") ?? string.Empty;
            
        if (type.GetProperty("PrepTimeMinutes") is not null)
            entity.PrepTimeMinutes = GetPropertyValue<int>(domainRecipe, "PrepTimeMinutes");
            
        if (type.GetProperty("CookTimeMinutes") is not null)
            entity.CookTimeMinutes = GetPropertyValue<int>(domainRecipe, "CookTimeMinutes");
            
        if (type.GetProperty("Servings") is not null)
            entity.Servings = GetPropertyValue<int>(domainRecipe, "Servings");

        // Handle collections by serializing to JSON
        if (type.GetProperty("Ingredients") is not null)
        {
            var ingredients = GetPropertyValue<IEnumerable<string>>(domainRecipe, "Ingredients");
            entity.IngredientsJson = JsonSerializer.Serialize(ingredients ?? new List<string>());
        }
        
        if (type.GetProperty("Instructions") is not null)
        {
            var instructions = GetPropertyValue<IEnumerable<string>>(domainRecipe, "Instructions");
            entity.InstructionsJson = JsonSerializer.Serialize(instructions ?? new List<string>());
        }
        
        if (type.GetProperty("Tags") is not null)
        {
            var tags = GetPropertyValue<IEnumerable<string>>(domainRecipe, "Tags");
            entity.TagsJson = JsonSerializer.Serialize(tags ?? new List<string>());
        }
        
        if (type.GetProperty("NutritionInfo") is not null)
        {
            var nutritionInfo = GetPropertyValue<Dictionary<string, string>>(domainRecipe, "NutritionInfo");
            entity.NutritionInfoJson = JsonSerializer.Serialize(nutritionInfo ?? new Dictionary<string, string>());
        }

        // Set default values
        entity.SourceProvider = "HelloFresh"; // Can be parameterized
        entity.IsActive = true;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        return entity;
    }

    /// <summary>
    /// Maps a RecipeEntity back to a domain object
    /// This is a generic method that can work with different domain types
    /// </summary>
    public static TDomainRecipe FromEntity<TDomainRecipe>(RecipeEntity entity) 
        where TDomainRecipe : class, new()
    {
        var domainRecipe = new TDomainRecipe();
        var type = typeof(TDomainRecipe);

        // Map basic properties if they exist in the domain type
        SetPropertyValue(domainRecipe, "Id", entity.Id, type);
        SetPropertyValue(domainRecipe, "Title", entity.Title, type);
        SetPropertyValue(domainRecipe, "Description", entity.Description, type);
        SetPropertyValue(domainRecipe, "ImageUrl", entity.ImageUrl, type);
        SetPropertyValue(domainRecipe, "SourceUrl", entity.SourceUrl, type);
        SetPropertyValue(domainRecipe, "PrepTimeMinutes", entity.PrepTimeMinutes, type);
        SetPropertyValue(domainRecipe, "CookTimeMinutes", entity.CookTimeMinutes, type);
        SetPropertyValue(domainRecipe, "Servings", entity.Servings, type);
        SetPropertyValue(domainRecipe, "CreatedAt", entity.CreatedAt, type);
        SetPropertyValue(domainRecipe, "UpdatedAt", entity.UpdatedAt, type);

        // Handle collections by deserializing from JSON
        if (type.GetProperty("Ingredients") is not null)
        {
            var ingredients = JsonSerializer.Deserialize<List<string>>(entity.IngredientsJson) ?? new List<string>();
            SetPropertyValue(domainRecipe, "Ingredients", ingredients, type);
        }
        
        if (type.GetProperty("Instructions") is not null)
        {
            var instructions = JsonSerializer.Deserialize<List<string>>(entity.InstructionsJson) ?? new List<string>();
            SetPropertyValue(domainRecipe, "Instructions", instructions, type);
        }
        
        if (type.GetProperty("Tags") is not null)
        {
            var tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson) ?? new List<string>();
            SetPropertyValue(domainRecipe, "Tags", tags, type);
        }
        
        if (type.GetProperty("NutritionInfo") is not null)
        {
            var nutritionInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.NutritionInfoJson) ?? new Dictionary<string, string>();
            SetPropertyValue(domainRecipe, "NutritionInfo", nutritionInfo, type);
        }

        return domainRecipe;
    }

    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property is null) return default;
        
        var value = property.GetValue(obj);
        return value is T typedValue ? typedValue : default;
    }

    private static void SetPropertyValue(object obj, string propertyName, object? value, Type type)
    {
        var property = type.GetProperty(propertyName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }
}
