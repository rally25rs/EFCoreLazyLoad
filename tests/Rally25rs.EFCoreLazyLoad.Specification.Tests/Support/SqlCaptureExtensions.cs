using Rally25rs.EFCoreLazyLoad.Tests.Support;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;

public static class SqlCaptureExtensions
{
    /// <summary>Awaits <paramref name="action" /> and returns its result with the commands it executed.</summary>
    public static async Task<(T Result, IReadOnlyList<string> Sql)> DuringAsync<T>(
        this SqlCapture capture, Func<Task<T>> action)
    {
        var before = capture.Count;
        var result = await action();
        return (result, capture.Commands.Skip(before).ToList());
    }
}
