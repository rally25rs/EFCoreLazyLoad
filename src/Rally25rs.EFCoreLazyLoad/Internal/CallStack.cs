using System.Diagnostics;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>Captures the call stack of the code that triggered a lazy load.</summary>
internal static class CallStack
{
    // Leading frames from these namespaces are plumbing between the navigation getter and our loader.
    private static readonly string[] PlumbingPrefixes =
    [
        "at Microsoft.EntityFrameworkCore.",
        "at Rally25rs.EFCoreLazyLoad.Internal.",
        "at Castle.", // EF Core lazy-loading proxies
    ];

    /// <summary>
    ///     The current call stack, formatted like <see cref="Exception.StackTrace" />, without the leading EF Core and
    ///     Rally25rs.EFCoreLazyLoad frames, so it starts at the navigation getter.
    /// </summary>
    public static string Capture()
    {
        var lines = new StackTrace(skipFrames: 1, fNeedFileInfo: true)
            .ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        var first = 0;
        while (first < lines.Length && IsPlumbing(lines[first]))
        {
            first++;
        }

        return string.Join(Environment.NewLine, lines, first, lines.Length - first);
    }

    private static bool IsPlumbing(string line)
    {
        var frame = line.TrimStart();
        foreach (var prefix in PlumbingPrefixes)
        {
            if (frame.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
