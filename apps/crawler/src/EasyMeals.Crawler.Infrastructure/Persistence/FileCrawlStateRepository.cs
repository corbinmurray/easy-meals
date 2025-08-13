using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// File-based implementation of ICrawlStateRepository
/// Saves crawl state to a JSON file for persistence between runs
/// </summary>
public class FileCrawlStateRepository : ICrawlStateRepository
{
    private readonly string _stateFilePath;
    private readonly ILogger<FileCrawlStateRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileCrawlStateRepository(string stateFilePath, ILogger<FileCrawlStateRepository> logger)
    {
        _stateFilePath = stateFilePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CrawlState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                _logger.LogInformation("No existing crawl state file found. Creating new state.");
                return new CrawlState();
            }

            var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
            var state = JsonSerializer.Deserialize<CrawlState>(json, JsonOptions);
            
            if (state is null)
            {
                _logger.LogWarning("Failed to deserialize crawl state. Creating new state.");
                return new CrawlState();
            }

            _logger.LogInformation("Loaded crawl state from {FilePath}. Pending URLs: {Count}", 
                _stateFilePath, state.PendingUrls.Count);
            
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crawl state from {FilePath}. Creating new state.", _stateFilePath);
            return new CrawlState();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveStateAsync(CrawlState state, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
            
            _logger.LogDebug("Saved crawl state to {FilePath}", _stateFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save crawl state to {FilePath}", _stateFilePath);
            return false;
        }
    }
}
