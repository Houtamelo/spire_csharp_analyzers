using System;
using System.Diagnostics;
using System.IO;
using DevTools;
using Xunit;

namespace DevTools.Tests;

public sealed class FileSystemToolsTests
{
    [Fact]
    public void Remove_ProtectsAgentConfigurationAndAllowsOrdinaryFiles()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var repo = Path.Combine(Path.GetTempPath(), $"spire-devtools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repo);

        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "init --quiet")
            {
                WorkingDirectory = repo,
                UseShellExecute = false,
                RedirectStandardError = true
            })!;
            git.WaitForExit();
            Assert.Equal(0, git.ExitCode);

            Directory.SetCurrentDirectory(repo);
            foreach (var directory in new[] { ".claude", ".codex", ".agents" })
            {
                Directory.CreateDirectory(directory);
                var file = Path.Combine(directory, "config.txt");
                File.WriteAllText(file, "keep");

                Assert.Contains("sensitive path", FileSystemTools.Remove(file));
                Assert.True(File.Exists(file));
            }

            File.WriteAllText("ordinary.txt", "remove");
            Assert.Contains("Deleted:", FileSystemTools.Remove("ordinary.txt"));
            Assert.False(File.Exists("ordinary.txt"));

            var outside = Path.Combine(Path.GetDirectoryName(repo)!, Path.GetFileName(repo) + "-outside.txt");
            File.WriteAllText(outside, "keep");
            try
            {
                Assert.Contains("outside repo root", FileSystemTools.Remove(outside));
                Assert.True(File.Exists(outside));
            }
            finally { File.Delete(outside); }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(repo, recursive: true);
        }
    }
}
