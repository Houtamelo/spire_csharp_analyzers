using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Spire.UnityTestHarness;
using Xunit.Abstractions;
using Xunit.Sdk;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ManifestExporter <repository-root> <configuration> <output-manifest>");
    return 2;
}

try
{
    var repositoryRoot = Path.GetFullPath(args[0]);
    var configuration = args[1];
    var outputManifest = Path.GetFullPath(args[2]);
    var suites = new[]
    {
        (Folder: "Houtamelo.Spire.Analyzers.Tests", Assembly: "Houtamelo.Spire.Analyzers.Tests"),
        (Folder: "Houtamelo.Spire.SourceGenerators.Tests", Assembly: "Houtamelo.Spire.SourceGenerators.Tests"),
        (Folder: "Houtamelo.Spire.PatternAnalysis.Tests", Assembly: "Houtamelo.Spire.PatternAnalysis.Tests"),
        (Folder: "DevTools.Tests", Assembly: "DevTools.Tests"),
        (Folder: "Houtamelo.Spire.GlobalConfig.EndToEnd", Assembly: "Spire.GlobalConfig.EndToEnd"),
    };
    var assemblyPaths = suites
        .Select(suite => Path.Combine(
            repositoryRoot,
            "tests",
            suite.Folder,
            "bin",
            configuration,
            "netstandard2.1",
            suite.Assembly + ".dll"))
        .ToArray();
    foreach (var path in assemblyPaths)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Portable test assembly is missing.", path);
    }

    var outputDirectory = Path.GetDirectoryName(outputManifest);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);

    var dataRoot = Path.Combine(outputDirectory ?? Directory.GetCurrentDirectory(), "DataRoot");
    if (Directory.Exists(dataRoot))
        Directory.Delete(dataRoot, recursive: true);
    Directory.CreateDirectory(dataRoot);
    foreach (var suite in suites)
    {
        var source = Path.Combine(
            repositoryRoot,
            "tests",
            suite.Folder,
            "bin",
            configuration,
            "netstandard2.1");
        CopyDirectory(source, Path.Combine(dataRoot, suite.Assembly));
    }

    var referenceRoot = ResolveReferenceRoot(repositoryRoot);
    Environment.SetEnvironmentVariable("SPIRE_UNITY_TEST_DATA_ROOT", dataRoot);
    Environment.SetEnvironmentVariable("SPIRE_UNITY_TEST_REFERENCE_ROOT", referenceRoot);

    var dependencyDirectory = Path.Combine(
        repositoryRoot,
        "tools",
        "UnityHarnessDependencies",
        "bin",
        configuration,
        "netstandard2.1");
    if (!Directory.Exists(dependencyDirectory))
        throw new DirectoryNotFoundException("The authoritative Unity harness dependency bundle is missing: " + dependencyDirectory);

    var resolutionDirectories = new[] { dependencyDirectory };
    AssemblyLoadContext.Default.Resolving += (_, requested) =>
    {
        var fileName = requested.Name + ".dll";
        foreach (var directory in resolutionDirectories)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
        }

        return null;
    };

    var assemblies = assemblyPaths
        .Select(path => AssemblyLoadContext.Default.LoadFromAssemblyPath(path))
        .ToArray();
    var discovery = await DiscoverIndependentlyAsync(assemblies);
    File.WriteAllText(outputManifest, XunitReflectionBridge.ExportDiscoveryManifest(discovery.Cases, discovery.PlatformExclusions));
    Console.WriteLine($"Discovered {discovery.Cases.Count} applicable xUnit cases for {XunitReflectionBridge.CurrentPlatform}; " +
        $"{discovery.PlatformExclusions.Count} explicitly platform-inapplicable cases.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static string ResolveReferenceRoot(string repositoryRoot)
{
    var configured = Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_REFERENCE_ROOT");
    if (!string.IsNullOrEmpty(configured))
    {
        if (!Directory.Exists(configured) || !File.Exists(Path.Combine(configured, "System.Runtime.dll")))
            throw new DirectoryNotFoundException("Configured .NET reference pack is incomplete: " + configured);
        return Path.GetFullPath(configured);
    }
    var prepared = Path.Combine(repositoryRoot, "ReferenceAssemblies", "net8.0");
    if (Directory.Exists(prepared) && Directory.EnumerateFiles(prepared, "*.dll").Any())
        return prepared;

    var packageRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget",
        "packages",
        "microsoft.netcore.app.ref");
    var candidates = Directory.Exists(packageRoot)
        ? Directory.EnumerateDirectories(packageRoot)
            .Select(path => new
            {
                Path = Path.Combine(path, "ref", "net8.0"),
                Version = path,
            })
            .Where(candidate => Directory.Exists(candidate.Path))
            .OrderByDescending(candidate => candidate.Version, StringComparer.Ordinal)
            .Select(candidate => candidate.Path)
        : Enumerable.Empty<string>();
    return candidates.FirstOrDefault()
        ?? throw new DirectoryNotFoundException("Could not locate a .NET 8 reference pack.");
}

static void CopyDirectory(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var file in Directory.EnumerateFiles(source))
        File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    foreach (var directory in Directory.EnumerateDirectories(source))
        CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
}

static async Task<XunitDiscoveryResult> DiscoverIndependentlyAsync(
    IEnumerable<Assembly> assemblies)
{
    var cases = new List<XunitReflectionCase>();
    var exclusions = new List<XunitPlatformExclusion>();
    foreach (var assembly in assemblies)
    {
        var collector = new DiscoveryCollector();
        var framework = new XunitTestFramework(new Spire.UnityTestHarness.NullMessageSink());
        var discoverer = framework.GetDiscoverer(new ReflectionAssemblyInfo(assembly));
        discoverer.Find(
            includeSourceInformation: false,
            collector,
            CreateDiscoveryOptions());
        await collector.Completed.Task.WaitAsync(TimeSpan.FromMinutes(2));

        if (collector.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"xUnit discovery failed for '{assembly.GetName().Name}': " +
                string.Join(Environment.NewLine, collector.Errors));
        }

        if (collector.Cases.Count == 0)
        {
            throw new InvalidOperationException(
                $"xUnit discovery found no test cases in '{assembly.GetName().Name}'.");
        }

        foreach (var testCase in collector.Cases)
        {
            var reflectionMethod = (testCase.TestMethod.Method as IReflectionMethodInfo)?.MethodInfo;
            var fixtureType = (testCase.TestMethod.TestClass.Class as IReflectionTypeInfo)?.Type;
            if (reflectionMethod == null || fixtureType == null)
            {
                throw new InvalidOperationException(
                    $"xUnit discovery returned a non-reflection test method for '{assembly.GetName().Name}'.");
            }

            var @case = new XunitReflectionCase(
                fixtureType,
                reflectionMethod,
                testCase.TestMethodArguments ?? Array.Empty<object?>(),
                rowIndex: null);
            var required = XunitReflectionBridge.GetRequiredPlatform(reflectionMethod);
            if (!string.IsNullOrEmpty(testCase.SkipReason))
            {
                if (required == null || required == XunitReflectionBridge.CurrentPlatform ||
                    testCase.SkipReason != XunitReflectionBridge.PlatformSkipReason(required))
                    throw new InvalidOperationException(
                        $"xUnit discovery returned an unexplained skipped test '{testCase.DisplayName}': {testCase.SkipReason}");
                exclusions.Add(new XunitPlatformExclusion(@case, required, testCase.SkipReason));
            }
            else
            {
                if (required != null && required != XunitReflectionBridge.CurrentPlatform)
                    throw new InvalidOperationException($"xUnit did not report the required platform exclusion for '{testCase.DisplayName}'.");
                cases.Add(@case);
            }
        }
    }

    return new XunitDiscoveryResult(
        cases.OrderBy(testCase => testCase.Identity, StringComparer.Ordinal).ToArray(),
        exclusions.OrderBy(item => item.Case.Identity, StringComparer.Ordinal).ToArray());
}

static ITestFrameworkDiscoveryOptions CreateDiscoveryOptions()
{
    var optionsType = typeof(XunitTestFramework).Assembly.GetType("Xunit.Sdk.TestFrameworkOptions");
    var forDiscovery = optionsType?.GetMethod(
        "ForDiscovery",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    if (forDiscovery?.Invoke(null, null) is ITestFrameworkDiscoveryOptions options)
        return options;

    // xUnit 2.9.3 does not expose TestFrameworkOptions.ForDiscovery publicly;
    // provide the same option bag for its discoverer's documented interface.
    var fallback = new ReflectionDiscoveryOptions();
    fallback.SetValue("xunit.discovery.PreEnumerateTheories", true);
    fallback.SetValue("xunit.discovery.SynchronousMessageReporting", true);
    return fallback;
}

sealed class ReflectionDiscoveryOptions : ITestFrameworkDiscoveryOptions
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    public TValue GetValue<TValue>(string name)
        => values.TryGetValue(name, out var value) && value is TValue typed
            ? typed
            : default!;

    public void SetValue<TValue>(string name, TValue value)
        => values[name] = value;
}

sealed class DiscoveryCollector : IMessageSink
{
    public List<ITestCase> Cases { get; } = new();

    public List<string> Errors { get; } = new();

    public TaskCompletionSource<bool> Completed { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case TestCaseDiscoveryMessage discovery:
                Cases.Add(discovery.TestCase);
                break;
            case DiscoveryCompleteMessage:
                Completed.TrySetResult(true);
                break;
            case IErrorMessage error:
                Errors.Add(error.ToString() ?? "Unknown xUnit discovery error.");
                break;
        }

        return true;
    }
}
