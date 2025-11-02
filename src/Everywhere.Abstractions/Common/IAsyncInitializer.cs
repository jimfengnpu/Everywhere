namespace Everywhere.Common;

/// <summary>
/// Smaller numbers are initialized first.
/// </summary>
public enum AsyncInitializerPriority
{
    Highest = int.MinValue,

    Database = 10,

    Settings = 100,
    AfterSettings = 101,

    Startup = int.MaxValue,
}

public interface IAsyncInitializer
{
    /// <summary>
    /// Smaller numbers are initialized first.
    /// </summary>
    AsyncInitializerPriority Priority { get; }

    Task InitializeAsync();
}