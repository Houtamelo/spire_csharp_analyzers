using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class MSBuildPolicyIsolationTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task LegacyUnityProject_UsesGeneratedCompilerPolicyAfterExamplesBoundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var boundaryPath = Path.Combine(
            repositoryRoot,
            "tests",
            "DevTools.Tests",
            "Fixtures",
            "MSBuildPolicy",
            "Directory.Build.props");
        Assert.True(File.Exists(boundaryPath), boundaryPath);

        using var fixture = PolicyFixture.Create(
            repositoryRoot,
            LegacyUnityProject,
            File.ReadAllText(boundaryPath));

        var beforeBoundary = await EvaluatePropertiesAsync(repositoryRoot, fixture.LegacyProjectPath);
        Assert.Equal("true", beforeBoundary.TreatWarningsAsErrors);
        Assert.Equal("enable", beforeBoundary.Nullable);

        fixture.InstallBoundary();
        var afterBoundary = await EvaluatePropertiesAsync(repositoryRoot, fixture.LegacyProjectPath);
        Assert.Equal("False", afterBoundary.TreatWarningsAsErrors);
        Assert.Equal(string.Empty, afterBoundary.Nullable);

        fixture.RemoveBoundary();
        var missingBoundary = await EvaluatePropertiesAsync(repositoryRoot, fixture.LegacyProjectPath);
        Assert.Equal("true", missingBoundary.TreatWarningsAsErrors);
        Assert.Equal("enable", missingBoundary.Nullable);
    }

    private const string LegacyUnityProject = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup>
            <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
            <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
            <OutputType>Library</OutputType>
            <AssemblyName>LegacyUnityFixture</AssemblyName>
            <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>
          </PropertyGroup>
          <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
            <WarningLevel>4</WarningLevel>
            <NoWarn>0169,0649</NoWarn>
            <AllowUnsafeBlocks>True</AllowUnsafeBlocks>
            <TreatWarningsAsErrors>False</TreatWarningsAsErrors>
          </PropertyGroup>
          <PropertyGroup>
            <NoConfig>true</NoConfig>
            <NoStdLib>true</NoStdLib>
          </PropertyGroup>
          <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
        </Project>
        """;

    [Fact]
    public async Task RegularSdkLibrary_ContinuesUsingRepositoryCompilerPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var libraryProjectPath = Path.Combine(repositoryRoot, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj");
        var properties = await EvaluatePropertiesAsync(repositoryRoot, libraryProjectPath);

        Assert.Equal("true", properties.TreatWarningsAsErrors);
        Assert.Equal("enable", properties.Nullable);
    }

    [Fact]
    public async Task TestProject_DisablesSourceControlQueriesAndPropagatesTheFlagToProjectReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "tests", "DevTools.Tests", "DevTools.Tests.csproj");
        var policy = await EvaluateSourceControlPolicyAsync(repositoryRoot, projectPath);

        Assert.Equal("false", policy.Property);
        Assert.NotEmpty(policy.ProjectReferenceAdditionalProperties);
        Assert.All(
            policy.ProjectReferenceAdditionalProperties,
            value => Assert.Equal("EnableSourceControlManagerQueries=false", value));
    }

    [Fact]
    public async Task ProductionProject_RetainsTheDefaultSourceControlQueryPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj");
        var policy = await EvaluateSourceControlPolicyAsync(repositoryRoot, projectPath);

        Assert.Equal("true", policy.Property);
        Assert.All(
            policy.ProjectReferenceAdditionalProperties,
            value => Assert.Equal(string.Empty, value));
    }

    private static async Task<MSBuildProperties> EvaluatePropertiesAsync(string repositoryRoot, string projectPath)
    {
        var result = await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            new[]
            {
                "msbuild",
                projectPath,
                "-getProperty:TreatWarningsAsErrors",
                "-getProperty:Nullable",
                "-nologo",
                "-v:q",
            },
            repositoryRoot,
            ProcessTimeout);

        Assert.True(
            result.ExitCode == 0,
            $"MSBuild property evaluation failed for {projectPath}.\n{result.StandardOutput}\n{result.StandardError}");

        using var document = JsonDocument.Parse(result.StandardOutput);
        var properties = document.RootElement.GetProperty("Properties");
        return new MSBuildProperties(
            properties.GetProperty("TreatWarningsAsErrors").GetString() ?? string.Empty,
            properties.GetProperty("Nullable").GetString() ?? string.Empty);
    }

    private static async Task<SourceControlPolicy> EvaluateSourceControlPolicyAsync(
        string repositoryRoot,
        string projectPath)
    {
        var result = await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            new[]
            {
                "msbuild",
                projectPath,
                "-getProperty:EnableSourceControlManagerQueries",
                "-getItem:ProjectReference",
                "-nologo",
                "-v:q",
            },
            repositoryRoot,
            ProcessTimeout);

        Assert.True(
            result.ExitCode == 0,
            $"MSBuild source-control policy evaluation failed for {projectPath}.\n{result.StandardOutput}\n{result.StandardError}");

        using var document = JsonDocument.Parse(result.StandardOutput);
        var property = document.RootElement
            .GetProperty("Properties")
            .GetProperty("EnableSourceControlManagerQueries")
            .GetString() ?? string.Empty;
        var references = document.RootElement
            .GetProperty("Items")
            .TryGetProperty("ProjectReference", out var projectReferences)
            ? projectReferences.EnumerateArray()
                .Select(item => item.TryGetProperty("AdditionalProperties", out var additionalProperties)
                    ? additionalProperties.GetString() ?? string.Empty
                    : string.Empty)
                .ToArray()
            : Array.Empty<string>();
        return new SourceControlPolicy(property, references);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx")) &&
                    File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for MSBuild policy tests.");
    }

    private sealed class PolicyFixture : IDisposable
    {
        private PolicyFixture(string root, string legacyProject, string boundary)
        {
            Root = root;
            BoundaryPath = Path.Combine(root, "examples", "Directory.Build.props");
            LegacyProjectPath = Path.Combine(root, "examples", "LegacyUnity.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(LegacyProjectPath)!);
            File.WriteAllText(LegacyProjectPath, legacyProject);
            Boundary = boundary;
        }

        private string Boundary { get; }

        public string Root { get; }
        public string BoundaryPath { get; }
        public string LegacyProjectPath { get; }

        public static PolicyFixture Create(string repositoryRoot, string legacyProject, string boundary)
            => new(
                Path.Combine(repositoryRoot, "tmp", "msbuild-policy-tests", Guid.NewGuid().ToString("N")),
                legacyProject,
                boundary);

        public void InstallBoundary()
            => File.WriteAllText(BoundaryPath, Boundary);

        public void RemoveBoundary()
        {
            if (File.Exists(BoundaryPath))
                File.Delete(BoundaryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record MSBuildProperties(string TreatWarningsAsErrors, string Nullable);

    private sealed record SourceControlPolicy(string Property, string[] ProjectReferenceAdditionalProperties);
}
