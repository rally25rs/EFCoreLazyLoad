using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Rally25rs.EFCoreLazyLoad.Tests.Support;

/// <summary>Records the text of every command EF Core sends to the database.</summary>
public sealed class SqlCapture : DbCommandInterceptor
{
    private readonly Lock _lock = new();
    private readonly List<string> _commands = [];

    public IReadOnlyList<string> Commands
    {
        get
        {
            lock (_lock)
            {
                return [.. _commands];
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _commands.Count;
            }
        }
    }

    /// <summary>Runs <paramref name="action" /> and returns the commands it executed.</summary>
    public IReadOnlyList<string> During(Action action)
    {
        var before = Count;
        action();
        return Commands.Skip(before).ToList();
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command)
    {
        lock (_lock)
        {
            _commands.Add(command.CommandText);
        }
    }
}
