using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace DevTools.Tests;

public sealed class PublishDryRunTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);

    private static readonly PackageDefinition[] Packages =
    [
        new("Houtamelo.Spire", "src/Houtamelo.Spire/Houtamelo.Spire.csproj"),
        new("Houtamelo.Spire.Analyzers", "src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj"),
        new("Houtamelo.Spire.CodeFixes", "src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj")
    ];

    [Fact]
    public async Task PublishDryRun_BuildsTestsPacksAndInspectsAllPackages()
    {
        using var fixture = Fixture.Create("1.2.3", "1.2.3", "1.2.3");

        var result = await RunScriptAsync(
            fixture.Root,
            Path.Combine(fixture.Root, "tools", "publish-dryrun.sh"));

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, $"Publishing dry run failed with exit code {result.ExitCode}.\n{result.CombinedOutput}");

        foreach (var package in Packages)
        {
            var archivePath = Path.Combine(
                fixture.Root,
                Path.GetDirectoryName(package.ProjectPath)!,
                "bin",
                "Release",
                $"{package.Id}.1.2.3.nupkg");

            Assert.True(File.Exists(archivePath), $"Expected package was not created: {archivePath}\n{result.CombinedOutput}");
            AssertPackageContainsVersionAndAssembly(archivePath, package.Id, "1.2.3");
            Assert.Contains(package.Id, result.CombinedOutput, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PublishDryRun_FromOutsideRepositoryRootFailsBeforeProducingOutputs()
    {
        using var emptyDirectory = new TemporaryDirectory("spire-publish-empty");
        var scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "publish-dryrun.sh");

        var result = await RunScriptAsync(emptyDirectory.Path, scriptPath);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("src/Houtamelo.Spire/Houtamelo.Spire.csproj", result.CombinedOutput, StringComparison.Ordinal);
        Assert.True(
            result.CombinedOutput.Contains("repository root", StringComparison.OrdinalIgnoreCase) ||
            result.CombinedOutput.Contains("repo root", StringComparison.OrdinalIgnoreCase),
            $"Expected a repository-root diagnostic.\n{result.CombinedOutput}");
        AssertNoBuildOutputs(emptyDirectory.Path);
    }

    [Fact]
    public async Task PublishDryRun_WithMismatchedProjectVersionFailsBeforeBuild()
    {
        using var fixture = Fixture.Create("1.2.3", "1.2.4", "1.2.3");

        var result = await RunScriptAsync(
            fixture.Root,
            Path.Combine(fixture.Root, "tools", "publish-dryrun.sh"));

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Houtamelo.Spire.Analyzers", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("1.2.4", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("version", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--- restore ---", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        AssertNoBuildOutputs(fixture.Root);
    }

    private static async Task<ProcessResult> RunScriptAsync(string workingDirectory, string scriptPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "true",
                    ["MSBUILDDISABLENODEREUSE"] = "1"
                }
            }
        };
        process.StartInfo.ArgumentList.Add(scriptPath);

        Assert.True(process.Start(), $"Could not start publishing script: {scriptPath}");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var timedOut = false;

        using (var timeout = new CancellationTokenSource(ProcessTimeout))
        {
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                timedOut = true;
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
            }
        }

        var output = await standardOutput;
        var error = await standardError;
        return new ProcessResult(process.ExitCode, output, error, timedOut);
    }

    private static void AssertPackageContainsVersionAndAssembly(
        string archivePath,
        string packageId,
        string expectedVersion)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var nuspec = Assert.Single(
            archive.Entries,
            entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        using var nuspecStream = nuspec.Open();
        var document = XDocument.Load(nuspecStream);
        var version = document
            .Descendants()
            .Single(element => element.Name.LocalName == "version")
            .Value;

        Assert.Equal(expectedVersion, version);
        Assert.Contains(
            archive.Entries,
            entry => string.Equals(entry.Name, $"{packageId}.dll", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertNoBuildOutputs(string root)
    {
        Assert.Empty(Directory.EnumerateDirectories(root, "obj", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateDirectories(root, "bin", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateDirectories(root, "artifacts", SearchOption.AllDirectories));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "publish-dryrun.sh")) &&
                    File.Exists(Path.Combine(directory.FullName, "tools", "check-tag-version.sh")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root containing the publishing scripts.");
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static Fixture Create(string coreVersion, string analyzerVersion, string codeFixesVersion)
        {
            var fixture = new Fixture(Path.Combine(Path.GetTempPath(), $"spire-publish-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(Path.Combine(fixture.Root, "tools"));

            var repositoryRoot = FindRepositoryRoot();
            File.Copy(
                Path.Combine(repositoryRoot, "tools", "publish-dryrun.sh"),
                Path.Combine(fixture.Root, "tools", "publish-dryrun.sh"));
            File.Copy(
                Path.Combine(repositoryRoot, "tools", "check-tag-version.sh"),
                Path.Combine(fixture.Root, "tools", "check-tag-version.sh"));

            CreateProject(fixture.Root, Packages[0], coreVersion);
            CreateProject(fixture.Root, Packages[1], analyzerVersion);
            CreateProject(fixture.Root, Packages[2], codeFixesVersion);
            File.WriteAllText(
                Path.Combine(fixture.Root, "Spire.Analyzers.slnx"),
                "<Solution>\n" +
                "  <Folder Name=\"/src/\">\n" +
                "    <Project Path=\"src/Houtamelo.Spire/Houtamelo.Spire.csproj\" />\n" +
                "    <Project Path=\"src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj\" />\n" +
                "    <Project Path=\"src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj\" />\n" +
                "  </Folder>\n" +
                "</Solution>\n");

            return fixture;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static void CreateProject(string root, PackageDefinition package, string version)
        {
            var projectDirectory = Path.Combine(root, Path.GetDirectoryName(package.ProjectPath)!);
            Directory.CreateDirectory(projectDirectory);

            File.WriteAllText(
                Path.Combine(projectDirectory, Path.GetFileName(package.ProjectPath)),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <PropertyGroup>\n" +
                "    <TargetFramework>net10.0</TargetFramework>\n" +
                $"    <PackageId>{package.Id}</PackageId>\n" +
                $"    <Version>{version}</Version>\n" +
                "  </PropertyGroup>\n" +
                "</Project>\n");
            File.WriteAllText(
                Path.Combine(projectDirectory, "Marker.cs"),
                "namespace PublishFixture;\n\n" +
                "public static class Marker\n" +
                "{\n" +
                "    public static int Value => 42;\n" +
                "}\n");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string name)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed record PackageDefinition(string Id, string ProjectPath);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
    {
        public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
    }
}
