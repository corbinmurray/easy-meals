using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.HealthChecks;

public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoDbHealthCheck(IMongoDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ping the database to verify connectivity
            await _database.RunCommandAsync(
                (Command<BsonDocument>)"{ping:1}",
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB is responsive");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "MongoDB is not responsive",
                ex);
        }
    }
}