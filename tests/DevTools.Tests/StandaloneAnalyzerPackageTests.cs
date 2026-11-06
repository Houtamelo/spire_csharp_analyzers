using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class StandaloneAnalyzerPackageTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);
    private static readonly string BuildConfiguration = typeof(StandaloneAnalyzerPackageTests).Assembly
        .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;

    [Fact]
    public async Task StandaloneAnalyzerPackageShipsPatternAnalysisAndRunsAgainstAConsumer()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        var version = ReadVersion(repositoryRoot, "Houtamelo.Spire.Analyzers");

        var runtimePackage = await PackAsync(repositoryRoot, "Houtamelo.Spire", fixture.FeedPath);
        var analyzerPackage = await PackAsync(repositoryRoot, "Houtamelo.Spire.Analyzers", fixture.FeedPath);
        Assert.True(runtimePackage.ExitCode == 0, CombinedOutput(runtimePackage));
        Assert.True(analyzerPackage.ExitCode == 0, CombinedOutput(analyzerPackage));

        var runtimeNupkg = Path.Combine(fixture.FeedPath, $"Houtamelo.Spire.{version}.nupkg");
        var analyzerNupkg = Path.Combine(fixture.FeedPath, $"Houtamelo.Spire.Analyzers.{version}.nupkg");
        Assert.True(File.Exists(runtimeNupkg), CombinedOutput(runtimePackage));
        Assert.True(File.Exists(analyzerNupkg), CombinedOutput(analyzerPackage));
        AssertStandalonePackageContainsDependency(repositoryRoot, analyzerNupkg);

        CopyFrameworkPackagesToFeed(fixture.FeedPath);
        await RunConsumerAsync(
            repositoryRoot,
            fixture,
            runtimeNupkg,
            version,
            "kind-complete-consumer",
            CompleteSource,
            result =>
            {
                Assert.True(result.ExitCode == 0, CombinedOutput(result));
                Assert.DoesNotContain("AD0001", CombinedOutput(result), StringComparison.Ordinal);
                Assert.DoesNotContain("SPIRE009", CombinedOutput(result), StringComparison.Ordinal);
            });

        await RunConsumerAsync(
            repositoryRoot,
            fixture,
            runtimeNupkg,
            version,
            "kind-incomplete-consumer",
            IncompleteSource,
            result =>
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("SPIRE009", CombinedOutput(result), StringComparison.Ordinal);
                Assert.DoesNotContain("AD0001", CombinedOutput(result), StringComparison.Ordinal);
            });

        await RunConsumerAsync(
            repositoryRoot,
            fixture,
            runtimeNupkg,
            version,
            "nested-complete-consumer",
            NestedCompleteSource,
            result =>
            {
                Assert.True(result.ExitCode == 0, CombinedOutput(result));
                Assert.DoesNotContain("AD0001", CombinedOutput(result), StringComparison.Ordinal);
                Assert.DoesNotContain("SPIRE009", CombinedOutput(result), StringComparison.Ordinal);
            });

        await RunConsumerAsync(
            repositoryRoot,
            fixture,
            runtimeNupkg,
            version,
            "nested-incomplete-consumer",
            NestedIncompleteSource,
            result =>
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("SPIRE009", CombinedOutput(result), StringComparison.Ordinal);
                Assert.DoesNotContain("AD0001", CombinedOutput(result), StringComparison.Ordinal);
            });
    }

    private static void AssertStandalonePackageContainsDependency(string repositoryRoot, string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "analyzers/dotnet/cs/Houtamelo.Spire.Analyzers.dll");
        var dependency = archive.GetEntry("analyzers/dotnet/cs/Houtamelo.Spire.PatternAnalysis.dll");
        Assert.NotNull(dependency);

        var sourcePath = Path.Combine(repositoryRoot, "src", "Houtamelo.Spire.PatternAnalysis", "bin", BuildConfiguration, "netstandard2.0", "Houtamelo.Spire.PatternAnalysis.dll");
        Assert.True(File.Exists(sourcePath), sourcePath);
        using var packaged = dependency!.Open();
        using var source = File.OpenRead(sourcePath);
        using var hash = SHA256.Create();
        Assert.Equal(hash.ComputeHash(source), hash.ComputeHash(packaged));
    }

    private static async Task RunConsumerAsync(
        string repositoryRoot,
        PackageFixture fixture,
        string runtimePackage,
        string version,
        string name,
        string source,
        Action<ProcessResult> expect)
    {
        var projectRoot = Path.Combine(fixture.RootPath, name);
        Directory.CreateDirectory(projectRoot);
        var runtimePath = Path.Combine(projectRoot, "runtime", "Houtamelo.Spire.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
        using (var archive = ZipFile.OpenRead(runtimePackage))
        using (var runtime = archive.GetEntry("lib/netstandard2.0/Houtamelo.Spire.dll")!.Open())
        using (var output = File.Create(runtimePath))
        {
            runtime.CopyTo(output);
        }
        File.WriteAllText(Path.Combine(projectRoot, "Consumer.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>preview</LangVersion>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                <UseAppHost>false</UseAppHost>
                <RestoreSources>feed</RestoreSources>
                <RestorePackagesPath>cache</RestorePackagesPath>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Houtamelo.Spire.Analyzers" Version="{version}" />
                <Reference Include="Houtamelo.Spire" HintPath="runtime/Houtamelo.Spire.dll" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectRoot, "Program.cs"), source);

        var dotnet = new ToolContext(repositoryRoot).Dotnet;
        var restore = await ProcessRunner.RunAsync(dotnet, new[] { "restore", "Consumer.csproj", "--nologo", "--tl:off", "--source", "../feed" }, projectRoot, ProcessTimeout, ProcessEnvironment());
        Assert.True(restore.ExitCode == 0, CombinedOutput(restore));
        var build = await ProcessRunner.RunAsync(dotnet, new[] { "build", "Consumer.csproj", "-c", "Release", "--no-restore", "--nologo", "--tl:off" }, projectRoot, ProcessTimeout, ProcessEnvironment());
        expect(build);
    }

    private static async Task<ProcessResult> PackAsync(string repositoryRoot, string packageName, string output)
        => await ProcessRunner.RunAsync(new ToolContext(repositoryRoot).Dotnet, new[]
        {
            "pack", Path.Combine(repositoryRoot, "src", packageName, packageName + ".csproj"), "-c", BuildConfiguration,
            "--no-build", "--no-restore", "--nologo", "--tl:off", "--output", output,
        }, repositoryRoot, ProcessTimeout, ProcessEnvironment());

    private static Dictionary<string, string?> ProcessEnvironment() => new()
    {
        ["CI"] = "true",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBUILDTERMINALLOGGER"] = "off",
        ["EnableSourceControlManagerQueries"] = "false",
        ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(Path.GetTempPath(), "spire-nuget-http-cache"),
        ["NUGET_SCRATCH"] = Path.Combine(Path.GetTempPath(), "spire-nuget-scratch"),
    };

    private static void CopyFrameworkPackagesToFeed(string feedPath)
    {
        var packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach (var packageName in new[] { "microsoft.netcore.app.ref", "microsoft.aspnetcore.app.ref", "microsoft.netcore.app.host.linux-x64" })
        {
            var packageDirectory = Path.Combine(packageRoot, packageName);
            if (!Directory.Exists(packageDirectory))
                continue;

            foreach (var package in Directory.EnumerateDirectories(packageDirectory)
                         .SelectMany(path => Directory.EnumerateFiles(path, "*.nupkg")))
            {
                File.Copy(package, Path.Combine(feedPath, Path.GetFileName(package)), true);
            }
        }
    }

    private static string ReadVersion(string repositoryRoot, string packageName)
        => XDocument.Load(Path.Combine(repositoryRoot, "src", packageName, packageName + ".csproj"))
            .Descendants("Version").Select(element => element.Value).Single();

    private static string CombinedOutput(ProcessResult result) => result.StandardOutput + Environment.NewLine + result.StandardError;

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new InvalidOperationException("Could not locate the repository root for package tests.");
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root)
        {
            RootPath = root;
            FeedPath = Path.Combine(root, "feed");
            Directory.CreateDirectory(FeedPath);
        }
        public string RootPath { get; }
        public string FeedPath { get; }
        public static PackageFixture Create(string repositoryRoot) => new(Path.Combine(repositoryRoot, "tmp", "devtools-standalone-analyzer-package-tests", Guid.NewGuid().ToString("N")));
        public void Dispose() { if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true); }
    }

    private const string CompleteSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A();
            [Variant] public static partial Command B();
        }

        public static class Consumer
        {
            public static int Match(Command value) => value switch
            {
                { kind: Command.Kind.A } => 1,
                { kind: Command.Kind.B } => 2,
            };
        }
        """;

    private const string IncompleteSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A();
            [Variant] public static partial Command B();
        }

        public static class Consumer
        {
            public static int Match(Command value) => value switch
            {
                { kind: Command.Kind.A } => 1,
            };
        }
        """;

    private const string NestedCompleteSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }

        public static class Consumer
        {
            public static int Match(Command value) => value switch
            {
                { kind: Command.Kind.A, a: { flag: true } } => 1,
                { kind: Command.Kind.A, a: { flag: false } } => 2,
                { kind: Command.Kind.B, b: var _ } => 3,
            };
        }
        """;

    private const string NestedIncompleteSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }

        public static class Consumer
        {
            public static int Match(Command value) => value switch
            {
                { kind: Command.Kind.A, a: { flag: true } } => 1,
                { kind: Command.Kind.B, b: var _ } => 3,
            };
        }
        """;
}
