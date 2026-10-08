using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevTools;

internal static class ProcessCompatibility
{
    public static bool WaitForExit(Process process, TimeSpan timeout)
        => process.WaitForExit(checked((int)timeout.TotalMilliseconds));

    public static void AddArgument(ProcessStartInfo info, string argument)
    {
#if SPIRE_UNITY_TEST_HOST
        var text = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            text.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            text.Append(character);
            slashes = 0;
        }
        text.Append('\\', slashes * 2).Append('"');
        info.Arguments += (info.Arguments.Length == 0 ? "" : " ") + text;
#else
        info.ArgumentList.Add(argument);
#endif
    }

    public static async Task WaitForExitAsync(Process process, CancellationToken cancellation = default)
    {
#if SPIRE_UNITY_TEST_HOST
        while (!process.HasExited)
            await Task.Delay(10, cancellation).ConfigureAwait(false);
        process.WaitForExit();
#else
        await process.WaitForExitAsync(cancellation).ConfigureAwait(false);
#endif
    }

    public static void KillTree(Process process)
    {
#if SPIRE_UNITY_TEST_HOST
        if (Directory.Exists("/proc"))
        {
            var children = Directory.EnumerateDirectories("/proc")
                .Where(path => int.TryParse(Path.GetFileName(path), out _))
                .Select(path => (Path: path, Parent: ReadParent(path)))
                .Where(item => item.Parent == process.Id)
                .Select(item => int.Parse(Path.GetFileName(item.Path), CultureInfo.InvariantCulture))
                .ToArray();
            foreach (var id in children)
            {
                try
                {
                    using var child = Process.GetProcessById(id);
                    KillTree(child);
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
        }
        if (!process.HasExited)
            process.Kill();
#else
        process.Kill(entireProcessTree: true);
#endif
    }

#if SPIRE_UNITY_TEST_HOST
    private static int ReadParent(string directory)
    {
        try
        {
            var line = File.ReadLines(Path.Combine(directory, "status"))
                .FirstOrDefault(value => value.StartsWith("PPid:", StringComparison.Ordinal));
            return line != null && int.TryParse(line.Substring(5).Trim(), out var parent) ? parent : -1;
        }
        catch (IOException) { return -1; }
        catch (UnauthorizedAccessException) { return -1; }
    }
#endif

    public static byte[] HashData(byte[] data)
    {
        using var hash = SHA256.Create();
        return hash.ComputeHash(data);
    }

    public static string ToHexString(byte[] data)
        => string.Concat(data.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}
