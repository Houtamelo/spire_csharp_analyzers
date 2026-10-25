using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

#if !SPIRE_UNITY_TEST_HOST
using System.Runtime.Loader;
#endif

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

internal sealed class UnityGeneratorRun
{
    public UnityGeneratorRun(
        Compilation outputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> generatorDiagnostics,
        GeneratorDriverRunResult runResult)
    {
        OutputCompilation = outputCompilation;
        GeneratorDiagnostics = generatorDiagnostics;
        RunResult = runResult;
    }

    public Compilation OutputCompilation { get; }

    public ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics { get; }

    public GeneratorDriverRunResult RunResult { get; }

    public string GeneratedSource => string.Join(
        "\n",
        RunResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
}

internal static class UnityGeneratorTestHarness
{
    private const string KnownUnityEditorRoot =
        "/home/houtamelo/Unity/Hub/Editor/6000.5.6f1";

    public static string UnityEditorRoot => ResolveUnityEditorRoot();

    public static string ManagedRoot
        => Path.Combine(UnityEditorRoot, "Editor", "Data", "Managed");

    public static string UnityEngineRoot
        => Path.Combine(ManagedRoot, "UnityEngine");

    public static string UnityScriptingAssemblyPath
        => Path.Combine(UnityEngineRoot, "Unity.Scripting.dll");

    public static UnityGeneratorRun Run(
        string source,
        bool includeUnityReferences = true,
        bool includeEditorReferences = false,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool allowUnsafe = true)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(languageVersion)
            .WithPreprocessorSymbols(
                includeEditorReferences ? new[] { "UNITY_EDITOR" } : Array.Empty<string>());

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "unity-case.cs");

        var references = GetReferences(includeUnityReferences, includeEditorReferences);
        if (additionalReferences is not null)
            references = references.Concat(additionalReferences);
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: allowUnsafe,
            nullableContextOptions: NullableContextOptions.Enable);
        var compatibilityTree = languageVersion <= LanguageVersion.CSharp9
            ? null
            : TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
                parseOptions,
                references);

        var (compilation, optionsProvider) = CreateCompilation(
            source,
            syntaxTree,
            compatibilityTree,
            references,
            parseOptions,
            compilationOptions,
            globalOptions);

        var generator = new DiscriminatedUnionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        return new UnityGeneratorRun(
            outputCompilation,
            generatorDiagnostics,
            driver.GetRunResult());
    }

    private static (Compilation Compilation, AnalyzerConfigOptionsProvider? OptionsProvider)
        CreateCompilation(
            string source,
            SyntaxTree syntaxTree,
            SyntaxTree? compatibilityTree,
            IEnumerable<MetadataReference> references,
            CSharpParseOptions parseOptions,
            CSharpCompilationOptions compilationOptions,
            IReadOnlyDictionary<string, string>? globalOptions)
    {
        if (globalOptions is null)
        {
            return (
                CSharpCompilation.Create(
                    $"UnityGenerator_{Guid.NewGuid():N}",
                    compatibilityTree is null
                        ? new[] { syntaxTree }
                        : new[] { syntaxTree, compatibilityTree },
                    references,
                    compilationOptions),
                null);
        }

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(
                projectId,
                "UnityGeneratorProject",
                $"UnityGenerator_{Guid.NewGuid():N}",
                LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, parseOptions)
            .WithProjectCompilationOptions(projectId, compilationOptions);

        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);

        solution = solution.AddDocument(
            DocumentId.CreateNewId(projectId),
            "unity-case.cs",
            SourceText.From(source),
            filePath: "unity-case.cs");
        if (compatibilityTree is not null)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "__SpireUnityIsExternalInit.cs",
                compatibilityTree.GetText(),
                filePath: compatibilityTree.FilePath);
        }

        var config = "is_global = true\n" + string.Join(
            "\n",
            globalOptions.Select(option => option.Key + " = " + option.Value));
#if SPIRE_UNITY_TEST_HOST
        var configRoot = TestHarnessEnvironment.DataRoot;
#else
        var configRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tmp"));
#endif
        var configPath = Path.Combine(configRoot, "unity-test.globalconfig");
        solution = solution.AddAnalyzerConfigDocument(
            DocumentId.CreateNewId(projectId),
            ".globalconfig",
            SourceText.From(config),
            filePath: configPath);

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply Unity analyzer-config workspace changes.");

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var compilation = project.GetCompilationAsync().GetAwaiter().GetResult()!;
        return (compilation, project.AnalyzerOptions.AnalyzerConfigOptionsProvider);
    }

    public static void AssertUnityAssembliesAvailable(bool includeEditor = false)
    {
        Assert.True(
            File.Exists(Path.Combine(UnityEngineRoot, "UnityEngine.CoreModule.dll")),
            $"UnityEngine.CoreModule.dll was not found below '{UnityEngineRoot}'. " +
            "Set SPIRE_UNITY_EDITOR to the installed Unity 6000.5.6f1 root or Unity executable.");

        Assert.True(
            File.Exists(UnityScriptingAssemblyPath),
            $"Unity.Scripting.dll was not found below '{UnityEngineRoot}'. " +
            "Set SPIRE_UNITY_EDITOR to the installed Unity 6000.5.6f1 root or Unity executable.");

        if (includeEditor)
        {
            Assert.True(
                File.Exists(Path.Combine(ManagedRoot, "UnityEditor.dll")),
                $"UnityEditor.dll was not found below '{ManagedRoot}'. " +
                "Set SPIRE_UNITY_EDITOR to the installed Unity 6000.5.6f1 root or Unity executable.");
        }
    }

    public static void AssertNoGeneratorErrors(UnityGeneratorRun result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();

        Assert.True(
            diagnostics.Length == 0,
            "Unexpected generator diagnostics:\n" +
            string.Join(
                "\n",
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    public static void AssertNoCompilationErrors(UnityGeneratorRun result)
    {
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "Generated Unity compilation failed:\n" +
            string.Join(
                "\n",
                errors.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    public static string LoadFixture(string fixtureName)
    {
        var directory = FindFixtureDirectory();
        var path = Path.Combine(directory, fixtureName + ".cs.txt");

        Assert.True(File.Exists(path), $"Unity fixture was not found: {path}");
        return File.ReadAllText(path);
    }

    public static string GetFixtureDirectory()
    {
        return FindFixtureDirectory();
    }

    private static string FindFixtureDirectory()
    {
#if SPIRE_UNITY_TEST_HOST
        var unityDataDirectory = Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "Unity",
            "fixtures");
        if (Directory.Exists(unityDataDirectory))
            return unityDataDirectory;
#endif

#if !SPIRE_UNITY_TEST_HOST
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tests",
                "Houtamelo.Spire.SourceGenerators.Tests",
                "Unity",
                "fixtures");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        var currentDirectoryCandidate = Path.Combine(
            Directory.GetCurrentDirectory(),
            "tests",
            "Houtamelo.Spire.SourceGenerators.Tests",
            "Unity",
            "fixtures");
        if (Directory.Exists(currentDirectoryCandidate))
            return currentDirectoryCandidate;
#endif

        throw new DirectoryNotFoundException(
            "Could not locate tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/fixtures.");
    }

    private static string ResolveUnityEditorRoot()
    {
#if SPIRE_UNITY_TEST_HOST
        var configured = Environment.GetEnvironmentVariable("SPIRE_UNITY_EDITOR");
        var configuredRoot = FindUnityInstallRoot(configured);
        if (configuredRoot is not null)
            return configuredRoot;

        var runtimeEditorPath = TestHarnessEnvironment.RuntimeReferences
            .Select(reference => reference.Display)
            .FirstOrDefault(path =>
                path is not null &&
                string.Equals(
                    Path.GetFileName(path),
                    "UnityEditor.dll",
                    StringComparison.OrdinalIgnoreCase));
        var runtimeRoot = FindUnityInstallRoot(runtimeEditorPath);
        if (runtimeRoot is not null)
            return runtimeRoot;

        return KnownUnityEditorRoot;
#else
        var configured = Environment.GetEnvironmentVariable("SPIRE_UNITY_EDITOR");
        if (string.IsNullOrWhiteSpace(configured))
            return KnownUnityEditorRoot;

        var path = Path.GetFullPath(configured);
        if (File.Exists(path) &&
            string.Equals(Path.GetFileNameWithoutExtension(path), "Unity", StringComparison.OrdinalIgnoreCase))
        {
            var editorDirectory = Directory.GetParent(path);
            var installRoot = editorDirectory?.Parent;
            if (installRoot is not null &&
                File.Exists(Path.Combine(installRoot.FullName, "Editor", "Data", "Managed", "UnityEditor.dll")))
            {
                return installRoot.FullName;
            }
        }

        if (File.Exists(Path.Combine(path, "Editor", "Data", "Managed", "UnityEditor.dll")))
            return path;

        if (File.Exists(Path.Combine(path, "Data", "Managed", "UnityEditor.dll")))
            return Directory.GetParent(path)?.FullName ?? path;

        return path;
#endif
    }

#if SPIRE_UNITY_TEST_HOST
    private static string? FindUnityInstallRoot(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        var fullPath = Path.GetFullPath(configuredPath);
        var directory = File.Exists(fullPath)
            ? Directory.GetParent(fullPath)
            : new DirectoryInfo(fullPath);
        while (directory is not null)
        {
            if (IsUnityInstallRoot(directory.FullName))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsUnityInstallRoot(string path)
    {
        var managedRoot = Path.Combine(path, "Editor", "Data", "Managed");
        var editorAssembly = Path.Combine(managedRoot, "UnityEditor.dll");
        var coreModule = Path.Combine(
            managedRoot,
            "UnityEngine",
            "UnityEngine.CoreModule.dll");
        return File.Exists(editorAssembly) && File.Exists(coreModule);
    }
#endif

    internal static IEnumerable<MetadataReference> GetReferences(
        bool includeUnityReferences,
        bool includeEditorReferences)
    {
#if SPIRE_UNITY_TEST_HOST
        var references = (includeUnityReferences
                ? TestHarnessEnvironment.RuntimeReferences
                : HostFrameworkReferences())
            .Where(reference => !IsUnityEditorReference(reference))
            .Append(
                MetadataReference.CreateFromFile(
                    typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location))
            .ToList();
#else
        var references = (includeUnityReferences
                ? TestHarnessEnvironment.RuntimeReferences
                : TestHarnessEnvironment.FrameworkReferences)
            .AsEnumerable()
            .Append(
            MetadataReference.CreateFromFile(
                typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location));
#endif

        if (!includeUnityReferences)
            return references;

        AssertUnityAssembliesAvailable(includeEditorReferences);

#if !SPIRE_UNITY_TEST_HOST
        var unityReferences = Directory.EnumerateFiles(UnityEngineRoot, "UnityEngine*.dll")
            .Append(UnityScriptingAssemblyPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

        references = references.Concat(unityReferences);
#else
        AddReferenceFiles(
            references,
            Directory.EnumerateFiles(UnityEngineRoot, "UnityEngine*.dll")
                .Append(UnityScriptingAssemblyPath));
#endif

        if (includeEditorReferences)
        {
#if !SPIRE_UNITY_TEST_HOST
            references = references.Append(
                MetadataReference.CreateFromFile(
                    Path.Combine(ManagedRoot, "UnityEditor.dll")));
#else
            AddReferenceFiles(
                references,
                Directory.EnumerateFiles(ManagedRoot, "UnityEditor*.dll")
                    .Concat(Directory.EnumerateFiles(UnityEngineRoot, "UnityEditor*.dll"))
                    .Where(path => !string.Equals(
                        Path.GetFileName(path),
                        "UnityEditor.dll",
                        StringComparison.OrdinalIgnoreCase)));
            AddAliasedReference(
                references,
                Path.Combine(ManagedRoot, "UnityEditor.dll"));
#endif
        }

        return references;
    }

#if SPIRE_UNITY_TEST_HOST
    private static IEnumerable<MetadataReference> HostFrameworkReferences()
    {
        var sdkRoot = Path.Combine(
            UnityEditorRoot,
            "Editor",
            "Data",
            "DotNetSdk",
            "sdk");
        if (!Directory.Exists(sdkRoot))
            return TestHarnessEnvironment.FrameworkReferences;

        var frameworkRoot = Directory.EnumerateDirectories(sdkRoot)
            .Select(path => Path.Combine(path, "TestHostNetFramework"))
            .Where(Directory.Exists)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault();
        if (frameworkRoot is null)
            return TestHarnessEnvironment.FrameworkReferences;

        // Unity's TestHostNetFramework facade carries netstandard 2.0, matching
        // Houtamelo.Spire's netstandard2.0 reference. Do not import its bundled
        // Newtonsoft/TestPlatform assemblies; JSON tests add their actual runtime
        // assemblies explicitly. Unity's loaded Mono core assemblies supply
        // framework types forwarded by the netstandard facades, such as
        // EditorBrowsableState, without introducing a second reference-only
        // mscorlib/System pair.
        var frameworkPaths = Directory.EnumerateFiles(frameworkRoot, "*.dll")
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Microsoft.Win32.Primitives.dll", StringComparison.OrdinalIgnoreCase);
            })
            .Concat(TestHarnessEnvironment.RuntimeReferences
                .Select(reference => reference.Display)
                .Where(path => path is not null)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return string.Equals(name, "mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "System.dll", StringComparison.OrdinalIgnoreCase);
                })
                .Cast<string>())
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

        return frameworkPaths;
    }

    private const string UnityEditorFacadeAlias = "__SpireUnityEditorFacade";

    private static bool IsUnityEditorReference(MetadataReference reference)
        => reference.Display is not null &&
            Path.GetFileName(reference.Display)
                .StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase);

    private static void AddReferenceFiles(
        List<MetadataReference> references,
        IEnumerable<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) || references.Any(reference =>
                    string.Equals(reference.Display, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(path));
        }
    }

    private static void AddAliasedReference(
        List<MetadataReference> references,
        string path)
    {
        if (!File.Exists(path) || references.Any(reference =>
                string.Equals(reference.Display, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Unity 6 keeps a legacy UnityEditor.dll facade beside modules that define
        // the same editor types. Keep its metadata available without making those
        // duplicate definitions compete with the global CoreModule references.
        references.Add(
            MetadataReference.CreateFromFile(path)
                .WithAliases(ImmutableArray.Create(UnityEditorFacadeAlias)));
    }
#endif

    internal static Assembly EmitAndLoad(Compilation compilation, string description)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            description + " could not be emitted:\n" + string.Join("\n", emit.Diagnostics));

#if SPIRE_UNITY_TEST_HOST
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
#else
        AssemblyLoadContext.Default.Resolving -= ResolveUnityAssembly;
        AssemblyLoadContext.Default.Resolving += ResolveUnityAssembly;
        return Assembly.Load(stream.ToArray());
#endif
    }

#if !SPIRE_UNITY_TEST_HOST
    private static Assembly? ResolveUnityAssembly(
        AssemblyLoadContext context,
        AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null)
            return null;

        var runtimeAssembly = typeof(Houtamelo.Spire.UnitySerializationPayloadBase).Assembly;
        if (string.Equals(name, runtimeAssembly.GetName().Name, StringComparison.Ordinal))
            return runtimeAssembly;

        if (!name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(name, "Unity.Scripting", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var roots = name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)
            ? new[] { ManagedRoot, UnityEngineRoot }
            : new[] { UnityEngineRoot };
        foreach (var root in roots)
        {
            var path = Path.Combine(root, name + ".dll");
            if (File.Exists(path))
                return context.LoadFromAssemblyPath(path);
        }

        return null;
    }
#endif
}
