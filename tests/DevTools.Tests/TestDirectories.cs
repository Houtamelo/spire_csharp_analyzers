using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DevTools.Tests;

internal static class TestDirectories
{
    internal static void DeleteOwned(string path)
    {
        var timer = Stopwatch.StartNew();
        while (Directory.Exists(path))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(5)) { }
            catch (UnauthorizedAccessException) when (timer.Elapsed < TimeSpan.FromSeconds(5)) { }
            Thread.Sleep(20);
        }
    }
}
