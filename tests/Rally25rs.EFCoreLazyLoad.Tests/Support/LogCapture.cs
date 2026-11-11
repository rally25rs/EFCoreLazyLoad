using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Tests.Support;

public sealed record LogEntry(string Category, LogLevel Level, EventId EventId, string Message);

/// <summary>An <see cref="ILoggerProvider" /> that keeps every entry written through it.</summary>
public sealed class LogCapture : ILoggerProvider
{
    public const string LazyLoadCategory = "Rally25rs.EFCoreLazyLoad";

    private readonly Lock _lock = new();
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Entries written by Rally25rs.EFCoreLazyLoad.</summary>
    public IReadOnlyList<LogEntry> LazyLoadEntries
        => Entries.Where(e => e.Category == LazyLoadCategory).ToList();

    public ILogger CreateLogger(string categoryName)
        => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    private sealed class Logger(LogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => owner.Add(new LogEntry(category, logLevel, eventId, formatter(state, exception)));
    }
}
