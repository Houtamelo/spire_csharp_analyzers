using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
#if !SPIRE_UNITY_TEST_HOST
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
#endif
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

[Collection("Unity customer delivery")]
public sealed class UnityCustomerDeliveryTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task BuildPackage_ExportsCustomerDocumentationUnderDocumentationTilde()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var documentationRoot = Path.Combine(fixture.OutputPath, "Documentation~");
        Assert.True(Directory.Exists(documentationRoot), documentationRoot);

        foreach (var source in UnityCustomerDeliveryTestSupport.DocumentationSourceFiles(repositoryRoot))
        {
            var expected = UnityCustomerDeliveryTestSupport.DocumentationPackagePath(
                repositoryRoot,
                source,
                fixture.OutputPath);
            Assert.True(File.Exists(expected), $"Missing shipped documentation: {expected}");
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(expected));
        }
    }

    [Fact]
    public async Task BuildPackage_DocumentationLocalMarkdownLinksResolveWithinPackage()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertDocumentationLinksResolve(fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_DocumentationContainsOfflineInstallTroubleshootingAndMigrationLimits()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var text = UnityCustomerDeliveryTestSupport.ReadDocumentationText(fixture.OutputPath);
        foreach (var required in new[]
        {
            "install",
            "Package Manager",
            "6000.5.6f1",
            "Rider",
            "URP",
            "17.5.0",
            "1.7.0",
            "serialization",
            "former",
            "variant",
            "troubleshoot",
        })
        {
            Assert.Contains(required, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task BuildPackage_DocumentationHasNoRepositorySpecificPathsOrLocalPackageReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertNoRepositorySpecificText(
            repositoryRoot,
            Directory.EnumerateFiles(
                Path.Combine(fixture.OutputPath, "Documentation~"),
                "*.md",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BuildPackage_ThirdPartyNoticesNamesPolySharpVersionAndLicense()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var notices = File.ReadAllText(Path.Combine(fixture.OutputPath, "Third Party Notices.md"));
        Assert.Contains("PolySharp", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.15.0", notices, StringComparison.Ordinal);
        Assert.Contains("02753db24d9685e54db06739eb63183d86eb5b62", notices, StringComparison.Ordinal);
        Assert.Contains("github.com/Sergio0694/PolySharp", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Copyright", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2022", notices, StringComparison.Ordinal);
        Assert.Contains("MIT", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Permission is hereby granted", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THE SOFTWARE IS PROVIDED", notices, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPackage_ThirdPartyNoticesMatchesEmbeddedPolySharpAttributes()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var notices = File.ReadAllText(Path.Combine(fixture.OutputPath, "Third Party Notices.md"));
        var typeNames = UnityCustomerDeliveryTestSupport.ReadCompilerCompatibilityTypeNames(
            fixture.OutputPath,
            UnityPackagingTests.ExpectedDlls);

        Assert.NotEmpty(typeNames);
        foreach (var typeName in typeNames)
            Assert.Contains(typeName, notices, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPackage_ThirdPartyNoticesDoesNotClaimUnshippedCompilerDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var notices = File.ReadAllText(Path.Combine(fixture.OutputPath, "Third Party Notices.md"));
        Assert.Contains("does not redistribute", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Roslyn", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Workspaces", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compiler-host", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("installed separately", notices, StringComparison.OrdinalIgnoreCase);

        UnityPackagingTests.AssertDllWhitelist(fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_CompilerExtensionsReferenceUnityRoslyn43OrOlderAndShipNoCompilerHosts()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityPackagingTests.AssertDllWhitelist(fixture.OutputPath);

        var compilerDlls = UnityPackagingTests.ExpectedDlls
            .Where(relative => relative.StartsWith("Analyzers/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(compilerDlls);

        var maximumSupportedRoslyn = new Version(4, 3, 0, 0);
        foreach (var relative in compilerDlls)
        {
            var path = Path.Combine(fixture.OutputPath, relative.Replace('/', Path.DirectorySeparatorChar));
            var assembly = Assembly.Load(File.ReadAllBytes(path));
            var compilerReferences = assembly.GetReferencedAssemblies()
                .Where(reference => reference.Name?.StartsWith(
                    "Microsoft.CodeAnalysis",
                    StringComparison.Ordinal) == true)
                .ToArray();

            Assert.NotEmpty(compilerReferences);
            foreach (var reference in compilerReferences)
            {
                Assert.True(
                    reference.Version <= maximumSupportedRoslyn,
                    $"{relative} references {reference.FullName}, above Unity's Roslyn 4.3 host baseline.");
            }
        }
    }

    [Fact]
    public async Task BuildPackage_BuildModeExportsRuntimeXmlAdjacentToRuntimeDll()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.CreateOutputOnly(repositoryRoot);
        var result = await RunPackageBuilderAsync(
            repositoryRoot,
            fixture.OutputPath,
            rootOverride: null,
            noBuild: false);

        AssertSuccess(result);
        var runtimeDll = Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.dll");
        var runtimeXml = Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.xml");
        var functionalDll = Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.Functional.dll");
        var functionalXml = Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.Functional.xml");
        Assert.True(File.Exists(runtimeDll), runtimeDll);
        Assert.True(File.Exists(runtimeXml), runtimeXml);
        Assert.True(File.Exists(functionalDll), functionalDll);
        Assert.True(File.Exists(functionalXml), functionalXml);
        Assert.NotEmpty(File.ReadAllText(runtimeXml));
        Assert.NotEmpty(File.ReadAllText(functionalXml));
        Assert.NotEqual(File.ReadAllText(runtimeDll), File.ReadAllText(runtimeXml));
        Assert.NotEqual(File.ReadAllText(functionalDll), File.ReadAllText(functionalXml));
    }

    [Fact]
    public async Task BuildPackage_RuntimeXmlIsWellFormedAndNamesRuntimeAssembly()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var xmlPath = Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.xml");
        var document = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
        Assert.Equal("doc", document.Root?.Name.LocalName);
        Assert.Equal("assembly", document.Root?.Element("assembly")?.Name.LocalName);
        Assert.Equal("Houtamelo.Spire", document.Root?.Element("assembly")?.Element("name")?.Value);
        var text = File.ReadAllText(xmlPath);
        Assert.DoesNotContain(repositoryRoot, text, StringComparison.Ordinal);
        Assert.DoesNotContain("bin/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("obj/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPackage_RuntimeXmlDocumentsEveryShippedPublicApiMember()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertPublicApiXmlCoverage(
            Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.dll"),
            Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.xml"));
    }

    [Fact]
    public async Task BuildPackage_RuntimeReflectionSmokeMatchesDocumentedSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertReflectionSmoke(
            Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.dll"),
            Path.Combine(fixture.OutputPath, "Runtime", "Houtamelo.Spire.xml"));
    }

    [Fact]
    public async Task BuildPackage_ManifestDeclaresUrpDependencyAndShowcaseSample()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixture.OutputPath, "package.json")));
        var manifest = document.RootElement;
        var dependencies = manifest.GetProperty("dependencies");
        Assert.Equal(
            new[] { "com.unity.modules.imgui", "com.unity.modules.uielements" },
            dependencies.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
        Assert.False(dependencies.TryGetProperty("com.unity.render-pipelines.universal", out _));
        Assert.False(dependencies.TryGetProperty("com.unity.test-framework", out _));

        var sample = manifest.GetProperty("samples").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "Samples~/Showcase");
        Assert.Equal("Unity Showcase (URP)", sample.GetProperty("displayName").GetString());
        Assert.Equal("Samples~/Showcase", sample.GetProperty("path").GetString());
        var description = sample.GetProperty("description").GetString()!;
        Assert.Contains("URP", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17.5.0", description, StringComparison.Ordinal);
        Assert.Contains("test framework", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.7.0", description, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPackage_ShowcaseSampleCopiesEveryTrackedUnityAsset()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var assetsRoot = UnityCustomerDeliveryTestSupport.SampleAssetsRoot(repositoryRoot);
        UnityCustomerDeliveryTestSupport.AssertSampleInventoryMatchesAssets(repositoryRoot);
        foreach (var source in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(assetsRoot, source);
            var target = Path.Combine(fixture.OutputPath, "Samples~", "Showcase", relative);
            Assert.True(File.Exists(target), $"Missing authored sample file: {target}");
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
        }

        var sampleReadme = Path.Combine(fixture.OutputPath, "Samples~", "Showcase", "README.md");
        Assert.True(File.Exists(sampleReadme), sampleReadme);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(repositoryRoot, "docs", "unity-package", "Showcase.md")),
            File.ReadAllBytes(sampleReadme));
    }

    [Fact]
    public async Task BuildPackage_ShowcaseSamplePreservesTrackedMetaGuids()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertAuthoredShowcaseMetaGuids(
            repositoryRoot,
            fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_ShowcaseSampleContainsNoRepositoryOrGeneratedProjectFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        var sampleRoot = Path.Combine(fixture.OutputPath, "Samples~", "Showcase");
        Assert.DoesNotContain("Assets", Directory.EnumerateDirectories(sampleRoot).Select(Path.GetFileName));
        Assert.DoesNotContain(
            "ProjectSettings",
            Directory.EnumerateDirectories(fixture.OutputPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName));
        Assert.DoesNotContain(
            "Packages",
            Directory.EnumerateDirectories(fixture.OutputPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName));
        Assert.DoesNotContain(
            "Library",
            Directory.EnumerateDirectories(fixture.OutputPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName));

        UnityCustomerDeliveryTestSupport.AssertNoRepositorySpecificText(
            repositoryRoot,
            Directory.EnumerateFiles(sampleRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BuildPackage_ShowcaseSampleHasNoBrokenLocalReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertShowcaseReferencesResolve(
            repositoryRoot,
            fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_ShowcaseSampleHasImportableUpmShape()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertImportableShowcaseShape(fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_ExportsOnlyAllowlistedCustomerFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertSuccess(result);
        UnityCustomerDeliveryTestSupport.AssertOnlyAllowlistedCustomerFiles(
            repositoryRoot,
            fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_RefusesUnrelatedExistingRootOrNestedFilesBeforeWriting()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.OutputPath);
        var rootSentinel = Path.Combine(fixture.OutputPath, "unrelated-sentinel.txt");
        var nestedSentinel = Path.Combine(fixture.OutputPath, "nested", "unrelated-sentinel.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedSentinel)!);
        File.WriteAllText(rootSentinel, "preserve this sentinel");
        File.WriteAllText(nestedSentinel, "preserve nested sentinel");

        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unrelated", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("preserve this sentinel", File.ReadAllText(rootSentinel));
        Assert.Equal("preserve nested sentinel", File.ReadAllText(nestedSentinel));
        Assert.Equal(
            new[] { "nested/unrelated-sentinel.txt", "unrelated-sentinel.txt" },
            Directory.EnumerateFiles(fixture.OutputPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(fixture.OutputPath, path).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path => path, StringComparer.Ordinal));
    }

    [Fact]
    public async Task BuildPackage_RejectsOutputInsideAnIsolatedRepositoryExamplesDirectoryBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var outputPath = Path.Combine(fixture.Root, "examples", "package");
        var result = await RunPackageBuilderAsync(repositoryRoot, outputPath, fixture.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("repository examples directory", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outputPath));
    }

    [Fact]
    public async Task BuildPackage_AllowsSiblingExamplesOtherDirectoryWhenExamplesIsAbsent()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var outputPath = Path.Combine(fixture.Root, "examples-other", "package");
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "examples")));

        var result = await RunPackageBuilderAsync(repositoryRoot, outputPath, fixture.Root);

        AssertSuccess(result);
        Assert.True(File.Exists(Path.Combine(outputPath, "package.json")), outputPath);
    }

    [Fact]
    public async Task BuildPackage_MissingRequiredAssemblyArtifactFailsBeforeCreatingOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        foreach (var relative in UnityCustomerDeliveryTestSupport.AssemblyArtifactPaths(BuildConfiguration()))
        {
            using var fixture = CustomerPackageFixture.Create(repositoryRoot);
            fixture.Remove(relative);

            var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

            AssertMissingInputFailure(result, fixture, relative);
        }
    }

    [Fact]
    public async Task BuildPackage_MissingLicenseOrDocumentationInputFailsWithoutPartialOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        foreach (var relative in UnityCustomerDeliveryTestSupport.RequiredDocumentationInputs(repositoryRoot))
        {
            using var fixture = CustomerPackageFixture.Create(repositoryRoot);
            fixture.Remove(relative);

            var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

            AssertMissingInputFailure(result, fixture, relative);
        }
    }

    [Fact]
    public async Task BuildPackage_MissingTrackedShowcaseAssetFailsWithoutPartialOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assetsRoot = UnityCustomerDeliveryTestSupport.SampleAssetsRoot(repositoryRoot);
        var inventory = Path.Combine(UnityCustomerDeliveryTestSupport.SampleRoot(repositoryRoot), "sample-files.txt");
        var inventoryRelative = Path.GetRelativePath(repositoryRoot, inventory);
        using (var inventoryFixture = CustomerPackageFixture.Create(repositoryRoot))
        {
            inventoryFixture.Remove(inventoryRelative);
            var inventoryResult = await RunPackageBuilderAsync(
                repositoryRoot,
                inventoryFixture.OutputPath,
                inventoryFixture.Root);
            AssertMissingInputFailure(inventoryResult, inventoryFixture, inventoryRelative);
        }

        foreach (var extension in new[] { ".cs", ".meta", ".unity", ".asset" })
        {
            var source = Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
                .First(path => path.EndsWith(extension, StringComparison.Ordinal));
            var relative = Path.GetRelativePath(repositoryRoot, source);
            using var fixture = CustomerPackageFixture.Create(repositoryRoot);
            fixture.Remove(relative);

            var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

            AssertMissingInputFailure(result, fixture, relative);
        }
    }

    [Theory]
    [InlineData("tests/UnityQuickStart/sample-files.txt")]
    [InlineData("tests/UnityQuickStart/Assets/QuickStart/QuickStart.cs")]
    [InlineData("tests/UnityQuickStart/Assets/QuickStart/QuickStart.cs.meta")]
    public async Task BuildPackage_MissingQuickStartInputFailsBeforeOutput(string relative)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        fixture.Remove(relative);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);
        AssertMissingInputFailure(result, fixture, relative);
    }

    [Fact]
    public async Task BuildPackage_MissingVersionProjectOrVersionElementFailsClearly()
    {
        var repositoryRoot = FindRepositoryRoot();
        foreach (var project in new[]
        {
            "Houtamelo.Spire",
            "Houtamelo.Spire.Functional",
            "Houtamelo.Spire.Analyzers",
            "Houtamelo.Spire.CodeFixes",
        })
        {
            var projectPath = $"src/{project}/{project}.csproj";

            using (var fixture = CustomerPackageFixture.Create(repositoryRoot))
            {
                fixture.Remove(projectPath);
                var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);
                AssertMissingInputFailure(result, fixture, projectPath);
            }

            using (var fixture = CustomerPackageFixture.Create(repositoryRoot))
            {
                fixture.RemoveVersionElement(projectPath);
                var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);
                AssertMissingInputFailure(result, fixture, projectPath + " Version");
            }

            using (var fixture = CustomerPackageFixture.Create(repositoryRoot))
            {
                fixture.SetVersion(projectPath, string.Empty);
                var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);
                AssertMissingInputFailure(result, fixture, projectPath + " empty Version");
            }
        }
    }

    [Fact]
    public async Task BuildPackage_MissingInputPreservesAnExistingValidBundleOnRerun()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CustomerPackageFixture.Create(repositoryRoot);
        var first = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);
        AssertSuccess(first);
        var before = UnityCustomerDeliveryTestSupport.CaptureFileDigests(fixture.OutputPath);

        var missing = "docs/rules/SPIRE001.md";
        fixture.Remove(missing);
        var second = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath, fixture.Root);

        AssertMissingInputFailure(second, fixture, missing, expectNoOutput: false);
        Assert.Equal(before, UnityCustomerDeliveryTestSupport.CaptureFileDigests(fixture.OutputPath));
    }

    private static async Task<ProcessResult> RunPackageBuilderAsync(
        string repositoryRoot,
        string outputPath,
        string? rootOverride,
        bool noBuild = true)
    {
        var launcherConfiguration = BuildConfiguration();
        var cliPath = Path.Combine(
            repositoryRoot,
            "tools",
            "Spire.Tool",
            "bin",
            launcherConfiguration,
            "net10.0",
            "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), $"Missing Spire.Tool CLI: {cliPath}");

        var arguments = new List<string>();
        if (rootOverride is not null)
        {
            arguments.Add("--root");
            arguments.Add(rootOverride);
        }

        arguments.Add("package");
        arguments.Add("--output");
        arguments.Add(outputPath);
        arguments.Add("--configuration");
        arguments.Add(BuildConfiguration());
        if (noBuild)
            arguments.Add("--no-build");

        var cliArguments = new[] { cliPath }.Concat(arguments).ToArray();
        string? logFile = null;
        if (!noBuild)
        {
            var logDirectory = Path.Combine(repositoryRoot, "tmp", "customer-delivery", "cli-logs");
            var fixtureName = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            logFile = Path.Combine(logDirectory, fixtureName + ".log");
            Directory.CreateDirectory(logDirectory);
            var environment = new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["NUGET_HTTP_CACHE_PATH"] = Environment.GetEnvironmentVariable("NUGET_HTTP_CACHE_PATH")
                    ?? Path.Combine(Path.GetTempPath(), "spire-nuget-http-cache"),
                ["SPIRE_DOTNET_HOST"] = Environment.GetEnvironmentVariable("SPIRE_DOTNET_HOST"),
                ["DOTNET_HOST_PATH"] = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
                ["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                ["DOTNET_ROOT_X64"] = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"),
                ["DOTNET_CLI_HOME"] = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME"),
                ["DOTNET_MULTILEVEL_LOOKUP"] = Environment.GetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP"),
                ["MSBUILDDISABLENODEREUSE"] = Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE"),
                ["MSBUILDNOINPROCNODE"] = Environment.GetEnvironmentVariable("MSBUILDNOINPROCNODE"),
                ["MSBUILD_EXE_PATH"] = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH"),
                ["MSBuildSDKsPath"] = Environment.GetEnvironmentVariable("MSBuildSDKsPath"),
            };
            File.WriteAllText(
                logFile + ".command.json",
                JsonSerializer.Serialize(
                    new
                    {
                        executable = new ToolContext(repositoryRoot).Dotnet,
                        arguments = cliArguments,
                        workingDirectory = repositoryRoot,
                        environment,
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
        }

        return await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            cliArguments,
            repositoryRoot,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
            },
            logFile: logFile);
    }

    private static void AssertSuccess(ProcessResult result)
        => Assert.True(result.ExitCode == 0, $"{result.StandardOutput}\n{result.StandardError}");

    private static void AssertMissingInputFailure(
        ProcessResult result,
        CustomerPackageFixture fixture,
        string missing,
        bool expectNoOutput = true)
    {
        var output = CombinedOutput(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            output.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("could not find", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("versions must match", StringComparison.OrdinalIgnoreCase),
            output);
        var missingPath = missing.Split(" Version", StringSplitOptions.None)[0]
            .Split(" empty Version", StringSplitOptions.None)[0];
        if (missing.Contains(" Version", StringComparison.Ordinal))
            Assert.Contains("version", output, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Contains(Path.GetFileName(missingPath), output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectNoOutput, !Directory.Exists(fixture.OutputPath));
    }

    private static string CombinedOutput(ProcessResult result)
        => $"{result.StandardOutput}\n{result.StandardError}";

    private static string BuildConfiguration()
#if SPIRE_UNITY_TEST_HOST
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ?? "Release";
#else
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
                ? "Release"
                : "Debug");
#endif

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

        throw new InvalidOperationException("Could not locate the repository root for Unity customer-delivery tests.");
    }

    private sealed class CustomerPackageFixture : IDisposable
    {
        private CustomerPackageFixture(string root)
        {
            Root = root;
            OutputPath = Path.Combine(root, "package");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string OutputPath { get; }

        public static CustomerPackageFixture Create(string repositoryRoot)
        {
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unity-customer-delivery-tests",
                Guid.NewGuid().ToString("N"));
            var fixture = new CustomerPackageFixture(root);
            fixture.CopyInputs(repositoryRoot);
            return fixture;
        }

        public static CustomerPackageFixture CreateOutputOnly(string repositoryRoot)
        {
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unity-customer-delivery-tests",
                Guid.NewGuid().ToString("N"));
            return new CustomerPackageFixture(root);
        }

        public void Remove(string relative)
        {
            var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                throw new FileNotFoundException("Fixture input is missing before removal.", path);
        }

        public void RemoveVersionElement(string relativeProject)
        {
            var path = Path.Combine(Root, relativeProject.Replace('/', Path.DirectorySeparatorChar));
            var document = XDocument.Load(path);
            document.Descendants("Version").Remove();
            document.Save(path);
        }

        public void SetVersion(string relativeProject, string version)
        {
            var path = Path.Combine(Root, relativeProject.Replace('/', Path.DirectorySeparatorChar));
            var document = XDocument.Load(path);
            var propertyGroup = document.Root?.Element("PropertyGroup")
                ?? throw new InvalidDataException("Fixture project has no PropertyGroup: " + path);
            var element = propertyGroup.Element("Version") ?? new XElement("Version");
            if (element.Parent is null)
                propertyGroup.Add(element);
            element.Value = version;
            document.Save(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private void CopyInputs(string repositoryRoot)
        {
            CopyFile(repositoryRoot, "LICENSE");
            foreach (var project in new[]
            {
                "Houtamelo.Spire",
                "Houtamelo.Spire.Functional",
                "Houtamelo.Spire.Analyzers",
                "Houtamelo.Spire.CodeFixes",
            })
            {
                CopyFile(repositoryRoot, $"src/{project}/{project}.csproj");
            }

            foreach (var relative in UnityCustomerDeliveryTestSupport.AssemblyArtifactPaths(BuildConfiguration()))
                CopyFile(repositoryRoot, relative);
            CopyFile(
                repositoryRoot,
                $"src/Houtamelo.Spire/bin/{BuildConfiguration()}/netstandard2.0/Houtamelo.Spire.xml");
            CopyFile(
                repositoryRoot,
                $"src/Houtamelo.Spire.Functional/bin/{BuildConfiguration()}/netstandard2.0/Houtamelo.Spire.Functional.xml");

            CopyDirectory(
                Path.Combine(repositoryRoot, "docs", "unity-package"),
                Path.Combine(Root, "docs", "unity-package"));

            var rulesSource = Path.Combine(repositoryRoot, "docs", "rules");
            foreach (var source in Directory.EnumerateFiles(rulesSource, "*.md"))
                CopyFile(repositoryRoot, Path.GetRelativePath(repositoryRoot, source));

            CopyDirectory(
                UnityCustomerDeliveryTestSupport.SampleAssetsRoot(repositoryRoot),
                Path.Combine(Root, "tests", "UnityPackageSample", "Assets"));
            CopyFile(repositoryRoot, "tests/UnityPackageSample/sample-files.txt");
            CopyDirectory(Path.Combine(repositoryRoot, "tests", "UnityQuickStart"), Path.Combine(Root, "tests", "UnityQuickStart"));
        }

        private void CopyFile(string repositoryRoot, string relative)
        {
            var source = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(source), $"Required fixture input is missing: {source}");
            var target = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }

        private static void CopyDirectory(string source, string target)
        {
            Assert.True(Directory.Exists(source), $"Required fixture directory is missing: {source}");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (var directory in Directory.EnumerateDirectories(source))
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}

[CollectionDefinition("Unity customer delivery", DisableParallelization = true)]
public sealed class UnityCustomerDeliveryCollectionDefinition
{
}

internal static class UnityCustomerDeliveryTestSupport
{
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex GuidPattern = new(
        @"\bguid:\s*(?<guid>[0-9a-fA-F]{32})\b",
        RegexOptions.Compiled);

    private static readonly Regex LocalPackageReferencePattern = new(
        @"\bfile:\s*(?:[./]|/|[A-Za-z]:[\\/])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] TextExtensions =
    [
        ".asset",
        ".asmdef",
        ".cs",
        ".json",
        ".md",
        ".meta",
        ".txt",
        ".unity",
    ];

    internal static IEnumerable<string> AssemblyArtifactPaths(string configuration)
    {
        yield return $"src/Houtamelo.Spire/bin/{configuration}/netstandard2.0/Houtamelo.Spire.dll";
        yield return $"src/Houtamelo.Spire.Functional/bin/{configuration}/netstandard2.0/Houtamelo.Spire.Functional.dll";
        yield return $"src/Houtamelo.Spire.Analyzers/bin/{configuration}/netstandard2.0/Houtamelo.Spire.Analyzers.dll";
        yield return $"src/Houtamelo.Spire.PatternAnalysis/bin/{configuration}/netstandard2.0/Houtamelo.Spire.PatternAnalysis.dll";
        yield return $"src/Houtamelo.Spire.CodeFixes/bin/{configuration}/netstandard2.0/Houtamelo.Spire.CodeFixes.dll";
    }

    internal static string SampleRoot(string repositoryRoot)
        => Path.Combine(repositoryRoot, "tests", "UnityPackageSample");

    internal static string SampleAssetsRoot(string repositoryRoot)
        => Path.Combine(SampleRoot(repositoryRoot), "Assets");

    internal static IEnumerable<string> DocumentationSourceFiles(string repositoryRoot)
    {
        var documentationRoot = Path.Combine(repositoryRoot, "docs", "unity-package", "Documentation~");
        foreach (var source in Directory.EnumerateFiles(documentationRoot, "*.md", SearchOption.AllDirectories))
            yield return source;

        foreach (var source in Directory.EnumerateFiles(
                     Path.Combine(repositoryRoot, "docs", "rules"),
                     "*.md",
                     SearchOption.TopDirectoryOnly))
        {
            yield return source;
        }
    }

    internal static string DocumentationPackagePath(
        string repositoryRoot,
        string source,
        string outputRoot)
    {
        var documentationRoot = Path.Combine(repositoryRoot, "docs", "unity-package", "Documentation~");
        if (source.StartsWith(documentationRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            var relative = Path.GetRelativePath(documentationRoot, source);
            return Path.Combine(outputRoot, "Documentation~", relative);
        }

        var rulesRoot = Path.Combine(repositoryRoot, "docs", "rules");
        var ruleRelative = Path.GetRelativePath(rulesRoot, source);
        return Path.Combine(outputRoot, "Documentation~", "Reference", "rules", ruleRelative);
    }

    internal static IEnumerable<string> RequiredDocumentationInputs(string repositoryRoot)
    {
        yield return "LICENSE";
        yield return "docs/unity-package/README.md";
        yield return "docs/unity-package/CHANGELOG.md";
        yield return "docs/unity-package/Third Party Notices.md";
        yield return "docs/unity-package/Showcase.md";
        yield return "docs/unity-package/documentation-files.txt";
        yield return $"src/Houtamelo.Spire/bin/{BuildConfiguration()}/netstandard2.0/Houtamelo.Spire.xml";
        yield return $"src/Houtamelo.Spire.Functional/bin/{BuildConfiguration()}/netstandard2.0/Houtamelo.Spire.Functional.xml";

        foreach (var source in DocumentationSourceFiles(repositoryRoot))
            yield return Path.GetRelativePath(repositoryRoot, source);
    }

    internal static Dictionary<string, string> CaptureFileDigests(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => RelativePath(root, path),
                path => DevTools.ProcessCompatibility.ToHexString(
                    DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal);

    internal static void AssertOnlyAllowlistedCustomerFiles(string repositoryRoot, string outputRoot)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);

        void AddGeneratedFile(string relative)
        {
            expected.Add(relative);
            if (!relative.EndsWith(".meta", StringComparison.Ordinal))
                expected.Add(relative + ".meta");
        }

        foreach (var rootFile in new[]
        {
            "package.json",
            "README.md",
            "LICENSE",
            "LICENSE.md",
            "CHANGELOG.md",
            "Third Party Notices.md",
        })
        {
            AddGeneratedFile(rootFile);
        }

        foreach (var relative in new[]
        {
            "Runtime/Houtamelo.Spire.dll",
            "Runtime/Houtamelo.Spire.Functional.dll",
            "Runtime/Houtamelo.Spire.xml",
            "Runtime/Houtamelo.Spire.Functional.xml",
            "Analyzers/Houtamelo.Spire.Analyzers.dll",
            "Analyzers/Houtamelo.Spire.PatternAnalysis.dll",
            "Analyzers/Houtamelo.Spire.CodeFixes.dll",
        })
        {
            AddGeneratedFile(relative);
        }

        expected.Add("Runtime.meta");
        expected.Add("Analyzers.meta");

        foreach (var source in DocumentationSourceFiles(repositoryRoot))
        {
            var relative = RelativePath(
                repositoryRoot,
                DocumentationPackagePath(repositoryRoot, source, repositoryRoot));
            expected.Add(relative);
        }

        var assetsRoot = UnityCustomerDeliveryTestSupport.SampleAssetsRoot(repositoryRoot);
        foreach (var source in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(assetsRoot, source).Replace(Path.DirectorySeparatorChar, '/');
            expected.Add("Samples~/Showcase/" + relative);
        }

        AddGeneratedFile("Samples~/Showcase/README.md");
        foreach (var relative in File.ReadAllLines(Path.Combine(repositoryRoot, "tests/UnityQuickStart/sample-files.txt")))
            expected.Add("Samples~/QuickStart/" + relative);

        var actual = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => RelativePath(outputRoot, path))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            expected.OrderBy(path => path, StringComparer.Ordinal),
            actual.OrderBy(path => path, StringComparer.Ordinal));
    }

    internal static void AssertGeneratedMetaGuids(string repositoryRoot, string outputRoot)
    {
        foreach (var metaPath in Directory.EnumerateFiles(outputRoot, "*.meta", SearchOption.AllDirectories))
        {
            var relative = RelativePath(outputRoot, metaPath);
            if (IsAuthoredShowcaseMeta(relative, repositoryRoot))
                continue;

            var logicalPath = relative[..^".meta".Length];
            var expected = DevTools.ProcessCompatibility.ToHexString(
                    DevTools.ProcessCompatibility.HashData(
                        Encoding.UTF8.GetBytes("Houtamelo.Spire.Unity/" + logicalPath)))
                .ToLowerInvariant()[..32];
            Assert.Equal("guid: " + expected, ReadGuidLine(metaPath));
        }
    }

    internal static bool IsAuthoredShowcaseMeta(string relative, string repositoryRoot)
    {
        if (relative.StartsWith("Samples~/QuickStart/", StringComparison.Ordinal) && relative.EndsWith(".meta", StringComparison.Ordinal))
            return File.Exists(Path.Combine(repositoryRoot, "tests/UnityQuickStart/Assets/QuickStart", relative["Samples~/QuickStart/".Length..]));
        const string prefix = "Samples~/Showcase/";
        if (!relative.StartsWith(prefix, StringComparison.Ordinal) ||
            !relative.EndsWith(".meta", StringComparison.Ordinal))
        {
            return false;
        }

        var sourceRelative = relative[prefix.Length..];
        var source = Path.Combine(
            SampleAssetsRoot(repositoryRoot),
            sourceRelative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(source);
    }

    internal static void AssertAuthoredShowcaseMetaGuids(string repositoryRoot, string outputRoot)
    {
        var sourceRoot = SampleAssetsRoot(repositoryRoot);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in Directory.EnumerateFiles(sourceRoot, "*.meta", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, source).Replace(Path.DirectorySeparatorChar, '/');
            var target = Path.Combine(
                outputRoot,
                "Samples~",
                "Showcase",
                relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(target), $"Missing authored sample metadata: {target}");

            var sourceGuid = ReadGuidLine(source)["guid: ".Length..];
            var targetGuid = ReadGuidLine(target)["guid: ".Length..];
            Assert.Equal(sourceGuid, targetGuid);
            Assert.True(seen.Add(targetGuid), $"Duplicate authored sample GUID: {targetGuid}");
        }
    }

    internal static void AssertSampleInventoryMatchesAssets(string repositoryRoot)
    {
        var sampleRoot = SampleRoot(repositoryRoot);
        var inventoryPath = Path.Combine(sampleRoot, "sample-files.txt");
        Assert.True(File.Exists(inventoryPath), inventoryPath);
        var inventory = File.ReadAllLines(inventoryPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var assets = Directory.EnumerateFiles(Path.Combine(sampleRoot, "Assets"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Path.Combine(sampleRoot, "Assets"), path)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(assets, inventory);
    }

    internal static string ReadDocumentationText(string outputRoot)
        => string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(outputRoot, "Documentation~"),
                    "*.md",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));

    internal static void AssertDocumentationLinksResolve(string outputRoot)
    {
        var documentationRoot = Path.Combine(outputRoot, "Documentation~");
        foreach (var file in Directory.EnumerateFiles(documentationRoot, "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in MarkdownLinkPattern.Matches(text))
            {
                var target = match.Groups["target"].Value.Trim('<', '>');
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var hash = target.IndexOf('#');
                var pathPart = hash < 0 ? target : target[..hash];
                var anchor = hash < 0 ? null : target[(hash + 1)..];
                var targetPath = pathPart.Length == 0
                    ? file
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, pathPart));
                Assert.True(
                    targetPath.StartsWith(documentationRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    string.Equals(targetPath, documentationRoot, StringComparison.Ordinal),
                    $"Documentation link escapes Documentation~: {file} -> {target}");
                Assert.True(File.Exists(targetPath), $"Broken documentation link: {file} -> {target}");

                if (anchor is not null && anchor.Length != 0)
                {
                    var targetText = File.ReadAllText(targetPath);
                    var anchors = MarkdownAnchors(targetText);
                    Assert.Contains(
                        Uri.UnescapeDataString(anchor),
                        anchors,
                        StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }

    internal static void AssertNoRepositorySpecificText(
        string repositoryRoot,
        IEnumerable<string> files)
    {
        var normalizedRoot = repositoryRoot.Replace('\\', '/');
        foreach (var file in files)
        {
            if (!TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(file);
            foreach (var forbidden in new[]
            {
                repositoryRoot,
                normalizedRoot,
                "examples/UnityShowcase",
                "tests/UnityPackageSample",
                "tmp/unity-package",
                ".csproj",
                ".sln",
            })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.False(
                LocalPackageReferencePattern.IsMatch(text),
                $"Local package reference leaked into {file}.");
        }
    }

    internal static void AssertShowcaseReferencesResolve(string repositoryRoot, string outputRoot)
    {
        var sampleRoot = Path.Combine(outputRoot, "Samples~", "Showcase");
        var localGuids = Directory.EnumerateFiles(sampleRoot, "*.meta", SearchOption.AllDirectories)
            .Select(path => ReadGuidLine(path)["guid: ".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceGuids = Directory.EnumerateFiles(
                SampleAssetsRoot(repositoryRoot),
                "*.meta",
                SearchOption.AllDirectories)
            .Select(path => ReadGuidLine(path)["guid: ".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(sampleRoot, "*", SearchOption.AllDirectories))
        {
            if (!TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(file);
            foreach (Match match in GuidPattern.Matches(text))
            {
                var guid = match.Groups["guid"].Value;
                if (sourceGuids.Contains(guid))
                    Assert.Contains(guid, localGuids, StringComparer.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain("Assets/", text, StringComparison.Ordinal);
            Assert.False(
                LocalPackageReferencePattern.IsMatch(text),
                $"Local package reference leaked into {file}.");
        }
    }

    internal static void AssertImportableShowcaseShape(string outputRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, "package.json")));
        var sample = document.RootElement.GetProperty("samples").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "Samples~/Showcase");
        var samplePath = sample.GetProperty("path").GetString()!;
        var sampleRoot = Path.Combine(outputRoot, samplePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(sampleRoot), sampleRoot);
        Assert.NotEmpty(Directory.EnumerateFiles(sampleRoot, "*.unity", SearchOption.AllDirectories));
        Assert.NotEmpty(Directory.EnumerateFiles(sampleRoot, "*.asset", SearchOption.AllDirectories));
        Assert.NotEmpty(Directory.EnumerateFiles(sampleRoot, "*.cs", SearchOption.AllDirectories));
        Assert.NotEmpty(Directory.EnumerateFiles(sampleRoot, "*.asmdef", SearchOption.AllDirectories));

        foreach (var file in Directory.EnumerateFiles(sampleRoot, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta", StringComparison.Ordinal))
                continue;
            Assert.True(File.Exists(file + ".meta"), $"Unity asset has no metadata: {file}");
        }
    }

    internal static IReadOnlyCollection<string> ReadCompilerCompatibilityTypeNames(
        string outputRoot,
        IEnumerable<string> relativeDlls)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relativeDll in relativeDlls)
        {
            var path = Path.Combine(outputRoot, relativeDll.Replace('/', Path.DirectorySeparatorChar));
#if SPIRE_UNITY_TEST_HOST
            var assembly = Assembly.Load(File.ReadAllBytes(path));
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type is not null).Cast<Type>().ToArray();
                if (types.Length == 0)
                    throw new InvalidOperationException(
                        "Could not reflect any types from copied package assembly: " + path,
                        exception);
            }

            foreach (var type in types)
            {
                if (string.Equals(type.Namespace, "System.Runtime.CompilerServices", StringComparison.Ordinal) &&
                    !string.Equals(type.Name, "<Module>", StringComparison.Ordinal))
                {
                    result.Add(type.Name);
                }
            }
#else
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var definition = metadata.GetTypeDefinition(handle);
                var @namespace = metadata.GetString(definition.Namespace);
                if (!string.Equals(@namespace, "System.Runtime.CompilerServices", StringComparison.Ordinal))
                    continue;
                var name = metadata.GetString(definition.Name);
                if (name.Length != 0 && name != "<Module>")
                    result.Add(name);
            }
#endif
        }

        return result;
    }

    internal static void AssertPublicApiXmlCoverage(string assemblyPath, string xmlPath)
    {
        var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
        var members = LoadXmlMembers(xmlPath);
        foreach (var type in assembly.GetExportedTypes().Where(IsConsumerType))
        {
            if (IsCompilerCompatibilityType(type) || IsHiddenInfrastructureType(type))
                continue;

            AssertXmlSummary(members, "T:" + XmlTypeName(type), type.FullName!);
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (constructor.GetParameters().Length == 0 && typeof(Attribute).IsAssignableFrom(type) &&
                    !members.ContainsKey(XmlMemberId(constructor)))
                {
                    continue;
                }

                AssertXmlSummary(members, XmlMemberId(constructor), constructor.ToString()!);
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!method.IsSpecialName && !method.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                    AssertXmlSummary(members, XmlMemberId(method), method.ToString()!);
            }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                AssertXmlSummary(members, XmlMemberId(property), property.ToString()!);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsSpecialName)
                    AssertXmlSummary(members, XmlMemberId(field), field.ToString()!);
            }
            foreach (var @event in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                AssertXmlSummary(members, XmlMemberId(@event), @event.ToString()!);
        }
    }

    internal static void AssertReflectionSmoke(string assemblyPath, string xmlPath)
    {
        var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
        var members = LoadXmlMembers(xmlPath);
        var expectedTypes = new List<string>
        {
            "Houtamelo.Spire.DiscriminatedUnionAttribute",
            "Houtamelo.Spire.ReferenceStorage",
            "Houtamelo.Spire.IDiscriminatedUnion" + "\u00601",
            "Houtamelo.Spire.SpireEnum" + "\u00601",
            "Houtamelo.Spire.SpireLINQ",
            "Houtamelo.Spire.UnitySerializationBridge",
        };
        expectedTypes.AddRange(Enumerable.Range(0, 9).Select(arity =>
            arity == 0
                ? "Houtamelo.Spire.IActionInliner"
                : "Houtamelo.Spire.IActionInliner" + "\u0060" + arity));
        expectedTypes.AddRange(Enumerable.Range(1, 9).Select(arity =>
            "Houtamelo.Spire.IFuncInliner" + "\u0060" + arity));

        foreach (var expectedType in expectedTypes)
        {
            var type = assembly.GetType(expectedType, throwOnError: false);
            Assert.NotNull(type);
            AssertXmlSummary(members, "T:" + XmlTypeName(type!), expectedType);
            foreach (var member in PublicDocumentedMembers(type!))
            {
                // UnitySerializationBridge's double-underscore methods are generated
                // infrastructure. They are intentionally allowed to remain undocumented;
                // its consumer-facing Prepare/Restore surface is still required above.
                if (type!.Name == "UnitySerializationBridge" && member.Name.StartsWith("__Spire", StringComparison.Ordinal))
                    continue;
                AssertXmlSummary(members, XmlMemberId(member), member.ToString()!);
            }
        }
    }

    private static IEnumerable<MemberInfo> PublicDocumentedMembers(Type type)
    {
        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            yield return constructor;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            if (!method.IsSpecialName && !method.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                yield return method;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            yield return property;
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            if (!field.IsSpecialName)
                yield return field;
        foreach (var @event in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            yield return @event;
    }

    private static bool IsConsumerType(Type type)
        => type.IsVisible && string.Equals(type.Namespace, "Houtamelo.Spire", StringComparison.Ordinal);

    private static bool IsCompilerCompatibilityType(Type type)
        => string.Equals(type.Namespace, "System.Runtime.CompilerServices", StringComparison.Ordinal);

    private static bool IsHiddenInfrastructureType(Type type)
        => type.Name is "UnitySerializationBridge" or "UnitySerializationPayloadBase";

    private static Dictionary<string, XElement> LoadXmlMembers(string xmlPath)
        => XDocument.Load(xmlPath)
            .Descendants("member")
            .Where(member => member.Attribute("name") is not null)
            .ToDictionary(
                member => member.Attribute("name")!.Value,
                member => member,
                StringComparer.Ordinal);

    private static void AssertXmlSummary(
        IReadOnlyDictionary<string, XElement> members,
        string id,
        string displayName)
    {
        Assert.True(members.TryGetValue(id, out var member), $"Missing XML documentation for {displayName} ({id}).");
        if (!string.IsNullOrWhiteSpace(member?.Element("summary")?.Value))
            return;

        var cref = member?.Element("inheritdoc")?.Attribute("cref")?.Value;
        if (string.IsNullOrWhiteSpace(cref))
        {
            Assert.Fail($"XML documentation has no summary for {displayName} ({id}).");
            return;
        }

        var inherited = members.TryGetValue(cref, out var direct)
            ? direct
            : members.FirstOrDefault(pair => pair.Key.EndsWith(cref, StringComparison.Ordinal)).Value;
        Assert.True(
            !string.IsNullOrWhiteSpace(inherited?.Element("summary")?.Value),
            $"XML documentation inheritdoc does not resolve to a summary for {displayName} ({id}).");
    }

    private static string XmlMemberId(MemberInfo member)
    {
        var declaringType = XmlTypeName(member.DeclaringType!);
        return member switch
        {
            ConstructorInfo constructor => "M:" + declaringType + ".#ctor" + XmlParameterList(constructor.GetParameters()),
            MethodInfo method => "M:" + declaringType + "." + method.Name +
                (method.IsGenericMethodDefinition ? "\u0060\u0060" + method.GetGenericArguments().Length : string.Empty) +
                XmlParameterList(method.GetParameters()),
            PropertyInfo property => "P:" + declaringType + "." + property.Name + XmlParameterList(property.GetIndexParameters()),
            FieldInfo => "F:" + declaringType + "." + member.Name,
            EventInfo => "E:" + declaringType + "." + member.Name,
            _ => throw new InvalidOperationException("Unsupported member kind: " + member.MemberType),
        };
    }

    private static string XmlTypeName(Type type)
    {
        if (type.IsByRef)
            return XmlTypeName(type.GetElementType()!) + "@";
        if (type.IsPointer)
            return XmlTypeName(type.GetElementType()!) + "*";
        if (type.IsArray)
        {
            var suffix = type.GetArrayRank() == 1
                ? "[]"
                : "[" + new string(',', type.GetArrayRank() - 1) + "]";
            return XmlTypeName(type.GetElementType()!) + suffix;
        }
        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "\u0060" : "\u0060\u0060") +
                type.GenericParameterPosition;
        }
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var definition = XmlTypeName(type.GetGenericTypeDefinition());
            var arity = definition.IndexOf('\u0060');
            if (arity >= 0)
                definition = definition[..arity];
            var arguments = string.Join(
                ",",
                type.GetGenericArguments().Select(XmlTypeName));
            return definition + "{" + arguments + "}";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string XmlParameterList(IReadOnlyCollection<ParameterInfo> parameters)
        => parameters.Count == 0
            ? string.Empty
            : "(" + string.Join(",", parameters.Select(parameter => XmlTypeName(parameter.ParameterType))) + ")";

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string ReadGuidLine(string path)
        => File.ReadLines(path).Single(line => line.StartsWith("guid: ", StringComparison.Ordinal));

    private static HashSet<string> MarkdownAnchors(string text)
    {
        var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;
            var heading = trimmed.TrimStart('#').Trim();
            if (heading.Length == 0)
                continue;
            var slug = new string(heading
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray());
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            anchors.Add(slug.Trim('-'));
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"\bid\s*=\s*[\""'](?<id>[^\""']+)[\""']",
                     RegexOptions.IgnoreCase))
        {
            anchors.Add(match.Groups["id"].Value);
        }

        return anchors;
    }

    private static string BuildConfiguration()
#if SPIRE_UNITY_TEST_HOST
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ?? "Release";
#else
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
                ? "Release"
                : "Debug");
#endif
}
