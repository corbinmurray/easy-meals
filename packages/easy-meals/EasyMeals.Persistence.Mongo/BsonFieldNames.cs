using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo;

/// <summary>
///     Provides type-safe access to BSON field names by extracting them from BsonElement attributes.
///     This eliminates string literals and provides compile-time safety for index creation.
/// </summary>
public static class BsonFieldNames
{
	/// <summary>
	///     Gets the BSON field name for a property using its BsonElement attribute or property name as fallback.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <typeparam name="TProperty">The property type.</typeparam>
	/// <param name="propertyExpression">Expression pointing to the property (e.g., d => d.CreatedAt).</param>
	/// <returns>The BSON field name (from BsonElement attribute or property name).</returns>
	public static string Get<TDocument, TProperty>(Expression<Func<TDocument, TProperty>> propertyExpression)
	{
		ArgumentNullException.ThrowIfNull(propertyExpression);

		if (propertyExpression.Body is not MemberExpression memberExpression)
			throw new ArgumentException("Expression must be a member access expression", nameof(propertyExpression));

		if (memberExpression.Member is not PropertyInfo propertyInfo)
			throw new ArgumentException("Expression must point to a property", nameof(propertyExpression));

		// Check for BsonElement attribute first
		var bsonElementAttr = propertyInfo.GetCustomAttribute<BsonElementAttribute>();
		if (bsonElementAttr != null && !string.IsNullOrEmpty(bsonElementAttr.ElementName))
			return bsonElementAttr.ElementName;

		// Fallback to camelCase conversion that handles acronyms and edge cases
		return ToCamelCase(propertyInfo.Name);
	}

	/// <summary>
	///     Converts a PascalCase string to camelCase, properly handling acronyms and consecutive capitals.
	///     Examples: "VARType" -> "varType", "IOStream" -> "ioStream", "CreatedAt" -> "createdAt"
	/// </summary>
	/// <param name="value">The PascalCase string to convert.</param>
	/// <returns>The camelCase version of the string.</returns>
	private static string ToCamelCase(string value)
	{
		if (string.IsNullOrEmpty(value))
			return value;

		if (value.Length == 1)
			return value.ToLowerInvariant();

		// Handle acronyms: if multiple consecutive uppercase letters, lowercase all but the last
		// "VARType" -> "varType", "IOStream" -> "ioStream"
		var uppercaseCount = 0;
		for (var i = 0; i < value.Length && char.IsUpper(value[i]); i++)
		{
			uppercaseCount++;
		}

		// All uppercase or single uppercase at start
		if (uppercaseCount == value.Length)
			return value.ToLowerInvariant();

		if (uppercaseCount == 1)
			return char.ToLowerInvariant(value[0]) + value[1..];

		// Multiple consecutive uppercase letters (acronym)
		// Keep all but the last uppercase letter lowercase
		return string.Concat(value[..(uppercaseCount - 1)].ToLowerInvariant(), value.AsSpan(uppercaseCount - 1));
	}
}