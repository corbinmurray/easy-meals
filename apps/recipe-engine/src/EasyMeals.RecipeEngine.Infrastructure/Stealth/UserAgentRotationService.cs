using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace EasyMeals.RecipeEngine.Infrastructure.Stealth;

/// <summary>
///     T098: Service for rotating user agent strings to avoid detection.
///     Uses round-robin selection from a configured list of realistic browser user agents.
/// </summary>
public class UserAgentRotationService : IUserAgentRotationService
{
    private readonly List<string> _userAgents;
    private int _currentIndex;
    private readonly object _lock = new();

    public UserAgentRotationService(IOptions<UserAgentOptions> options)
    {
        _userAgents = options.Value.UserAgents ?? new List<string>();
        _currentIndex = 0;
    }

    /// <summary>
    ///     Gets the next user agent string in round-robin fashion.
    ///     Thread-safe for concurrent access.
    /// </summary>
    /// <returns>A user agent string from the configured list</returns>
    /// <exception cref="InvalidOperationException">Thrown when the user agent list is empty</exception>
    public string GetNextUserAgent()
    {
        if (_userAgents.Count == 0)
            throw new InvalidOperationException("No user agents configured. Please add user agents to appsettings.json under UserAgents section.");

        lock (_lock)
        {
            string userAgent = _userAgents[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _userAgents.Count;
            return userAgent;
        }
    }
}

/// <summary>
///     Configuration options for user agent rotation.
/// </summary>
public class UserAgentOptions
{
    public List<string> UserAgents { get; set; } = new();
}