using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace Houtamelo.Spire.NuGet.Tests;

public sealed class NuGetConsumerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);

    private static readonly PackageDefinition[] Packages =
    [
        new("Houtamelo.Spire", "src/Houtamelo.Spire/Houtamelo.Spire.csproj"),
        new("Houtamelo.Spire.Analyzers", "src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj"),
        new("Houtamelo.Spire.CodeFixes", "src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj"),
    ];

    private static readonly string BuildConfiguration =
        typeof(NuGetConsumerTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Release";

    [Fact]
    public async Task PackedPackages_ContainTheirExpectedAssetsAndNoUnityAssemblyReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        var version = await PackAllAsync(repositoryRoot, fixture.FeedPath);

        foreach (var package in Packages)
        {
            var path = Path.Combine(fixture.FeedPath, $"{package.Id}.{version}.nupkg");
            Assert.True(File.Exists(path), $"Expected package was not created: {path}");

            using var archive = ZipFile.OpenRead(path);
            AssertExpectedAssets(package.Id, archive);

            foreach (var entry in archive.Entries.Where(entry =>
                         entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                using var entryStream = entry.Open();
                using var image = new MemoryStream();
                entryStream.CopyTo(image);
                image.Position = 0;
                using var peReader = new PEReader(image);
                var metadata = peReader.GetMetadataReader();
                var references = metadata.AssemblyReferences
                    .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name));
                Assert.DoesNotContain(
                    references,
                    reference => reference.StartsWith("Unity", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public async Task RuntimePackage_CompilesAndRunsSerializableCoreClrConsumerWithoutUnitySurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        var version = await PackAllAsync(repositoryRoot, fixture.FeedPath);
        CopyFrameworkPackagesToFeed(fixture.FeedPath);

        var (build, projectRoot) = await RunConsumerAsync(
            repositoryRoot,
            fixture,
            version,
            "serializable-coreclr-consumer",
            ValidConsumerSource,
            outputType: "Exe");

        Assert.True(build.ExitCode == 0, CombinedOutput(build));

        var generated = ReadGeneratedSources(projectRoot);
        Assert.NotEmpty(generated);
        Assert.All(generated, AssertContainsNoUnitySurface);
        Assert.Contains(
            generated,
            source => source.Contains("System.Serializable", StringComparison.Ordinal));

        var run = await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            new[] { "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build", "--no-restore", "--nologo", "--tl:off" },
            projectRoot,
            ProcessTimeout,
            ProcessEnvironment());
        Assert.True(run.ExitCode == 0, CombinedOutput(run));
    }

    [Fact]
    public async Task RuntimePackage_IncompleteSerializableSwitchFailsWithSpireDiagnosticOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        var version = await PackAllAsync(repositoryRoot, fixture.FeedPath);
        CopyFrameworkPackagesToFeed(fixture.FeedPath);

        var (build, projectRoot) = await RunConsumerAsync(
            repositoryRoot,
            fixture,
            version,
            "incomplete-serializable-consumer",
            IncompleteConsumerSource,
            outputType: "Library");

        var output = CombinedOutput(build);
        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains("SPIRE009", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AD0001", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SPIRE_DU013", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SPIRE_DU016", output, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityEngine", output, StringComparison.Ordinal);

        if (Directory.Exists(Path.Combine(projectRoot, "generated")))
            Assert.All(ReadGeneratedSources(projectRoot), AssertContainsNoUnitySurface);
    }

    private static async Task<string> PackAllAsync(string repositoryRoot, string feedPath)
    {
        var versions = Packages
            .Select(package => ReadVersion(repositoryRoot, package.Id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Single(versions);

        foreach (var package in Packages)
        {
            var result = await ProcessRunner.RunAsync(
                new ToolContext(repositoryRoot).Dotnet,
                new[]
                {
                    "pack", Path.Combine(repositoryRoot, package.ProjectPath),
                    "-c", BuildConfiguration,
                    "--no-build", "--no-restore", "--nologo", "--tl:off",
                    "--output", feedPath,
                },
                repositoryRoot,
                ProcessTimeout,
                ProcessEnvironment());
            Assert.True(result.ExitCode == 0, $"Packing {package.Id} failed.\n{CombinedOutput(result)}");
        }

        return versions[0];
    }

    private static async Task<(ProcessResult Build, string ProjectRoot)> RunConsumerAsync(
        string repositoryRoot,
        PackageFixture fixture,
        string version,
        string name,
        string source,
        string outputType)
    {
        var projectRoot = Path.Combine(fixture.RootPath, name);
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(Path.Combine(projectRoot, "Program.cs"), source);
        File.WriteAllText(
            Path.Combine(projectRoot, "Consumer.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>{outputType}</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>preview</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <UseAppHost>false</UseAppHost>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>generated</CompilerGeneratedFilesOutputPath>
                <RestoreSources>../feed</RestoreSources>
                <RestorePackagesPath>cache</RestorePackagesPath>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Houtamelo.Spire" Version="{version}" />
              </ItemGroup>
            </Project>
            """);

        var dotnet = new ToolContext(repositoryRoot).Dotnet;
        var restore = await ProcessRunner.RunAsync(
            dotnet,
            new[] { "restore", "Consumer.csproj", "--nologo", "--tl:off", "--source", "../feed" },
            projectRoot,
            ProcessTimeout,
            ProcessEnvironment());
        Assert.True(restore.ExitCode == 0, $"Consumer restore failed.\n{CombinedOutput(restore)}");

        var build = await ProcessRunner.RunAsync(
            dotnet,
            new[] { "build", "Consumer.csproj", "-c", "Release", "--no-restore", "--nologo", "--tl:off" },
            projectRoot,
            ProcessTimeout,
            ProcessEnvironment());
        return (build, projectRoot);
    }

    private static void AssertExpectedAssets(string packageId, ZipArchive archive)
    {
        var files = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        switch (packageId)
        {
            case "Houtamelo.Spire":
                Assert.Contains("lib/netstandard2.0/Houtamelo.Spire.dll", files);
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.Analyzers.dll", files);
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.PatternAnalysis.dll", files);
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.CodeFixes.dll", files);
                break;
            case "Houtamelo.Spire.Analyzers":
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.Analyzers.dll", files);
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.PatternAnalysis.dll", files);
                break;
            case "Houtamelo.Spire.CodeFixes":
                Assert.Contains("analyzers/dotnet/cs/Houtamelo.Spire.CodeFixes.dll", files);
                break;
            default:
                throw new InvalidOperationException($"Unexpected package '{packageId}'.");
        }
    }

    private static string[] ReadGeneratedSources(string projectRoot)
    {
        if (!Directory.Exists(projectRoot))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(projectRoot, "*.g.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains("generated", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToArray();
    }

    private static void AssertContainsNoUnitySurface(string source)
    {
        foreach (var token in new[]
        {
            "ISerializationCallbackReceiver",
            "UnitySerializationPayloadBase",
            "UnitySerializationBridge",
            "PropertyDrawer",
            "UnityEditor",
            "UnityEngine",
        })
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    private static void CopyFrameworkPackagesToFeed(string feedPath)
    {
        var packageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages");
        foreach (var packageName in new[]
        {
            "microsoft.netcore.app.ref",
            "microsoft.aspnetcore.app.ref",
            "microsoft.netcore.app.host.linux-x64",
        })
        {
            var packageDirectory = Path.Combine(packageRoot, packageName);
            if (!Directory.Exists(packageDirectory))
                continue;

            foreach (var package in Directory.EnumerateDirectories(packageDirectory)
                         .SelectMany(path => Directory.EnumerateFiles(path, "*.nupkg")))
            {
                File.Copy(package, Path.Combine(feedPath, Path.GetFileName(package)), overwrite: true);
            }
        }
    }

    private static Dictionary<string, string?> ProcessEnvironment() => new()
    {
        ["CI"] = "true",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBUILDTERMINALLOGGER"] = "off",
        ["EnableSourceControlManagerQueries"] = "false",
        ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(Path.GetTempPath(), "spire-nuget-http-cache"),
        ["NUGET_SCRATCH"] = Path.Combine(Path.GetTempPath(), "spire-nuget-scratch"),
    };

    private static string ReadVersion(string repositoryRoot, string packageId)
    {
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            packageId,
            packageId + ".csproj");
        var version = File.ReadAllLines(projectPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("<Version>", StringComparison.Ordinal))
            .Select(line => line["<Version>".Length..].Split('<')[0])
            .Single();
        return version;
    }

    private static string CombinedOutput(ProcessResult result)
        => result.StandardOutput + Environment.NewLine + result.StandardError;

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for NuGet consumer tests.");
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

        public static PackageFixture Create(string repositoryRoot)
            => new(Path.Combine(
                repositoryRoot,
                "tmp",
                "nuget-consumer-tests",
                Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed record PackageDefinition(string Id, string ProjectPath);

    private const string ValidConsumerSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Move(int distance);
            [Variant] public static partial Command Stop();
        }

        [Serializable, DiscriminatedUnion]
        public readonly partial struct ReadOnlyCommand
        {
            [Variant] public static partial ReadOnlyCommand Hold(int duration);
        }

        [Serializable, DiscriminatedUnion]
        public abstract partial record Result
        {
            public sealed partial record Success(int Value) : Result;
            public sealed partial record Failure(string Message) : Result;
        }

        public static class Program
        {
            private static int Match(Command command) => command switch
            {
                { kind: Command.Kind.Move, move: var (distance) } => distance,
                { kind: Command.Kind.Stop } => -1,
            };

            public static int Main()
            {
                Command converted = new CommandTypes.Variants.Move(7);
                if (Match(converted) != 7)
                    return 10;

                var readOnly = ReadOnlyCommand.Hold(11);
                if (readOnly.kind != ReadOnlyCommand.Kind.Hold || readOnly.hold.duration != 11)
                    return 11;

                Result result = new Result.Success(42);
                return result switch
                {
                    Result.Success { Value: 42 } => 0,
                    _ => 12,
                };
            }
        }
        """;

    private const string IncompleteConsumerSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Move(int distance);
            [Variant] public static partial Command Stop();
        }

        public static class Consumer
        {
            public static int Match(Command command) => command switch
            {
                { kind: Command.Kind.Move, move: var (distance) } => distance,
            };
        }
        """;
}
