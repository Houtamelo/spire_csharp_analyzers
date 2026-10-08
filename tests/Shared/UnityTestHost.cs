#if SPIRE_UNITY_TEST_HOST

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Spire.Tests.Hosting;

/// <summary>
/// Paths and metadata references supplied by the disposable Unity test host.
/// </summary>
public static class UnityTestHost
{
    private const string DataRootVariable = "SPIRE_UNITY_TEST_DATA_ROOT";
    private const string ReferenceRootVariable = "SPIRE_UNITY_TEST_REFERENCE_ROOT";

    private static readonly Lazy<ImmutableArray<MetadataReference>> CachedFrameworkReferences =
        new(LoadFrameworkReferences);

    private static readonly Lazy<ImmutableArray<MetadataReference>> CachedRuntimeReferences =
        new(LoadRuntimeReferences);

    /// <summary>
    /// Gets the copied test-data directory for the specified test assembly.
    /// </summary>
    public static string DataRoot(Assembly assembly)
    {
        var root = RequiredEnvironmentVariable(DataRootVariable);
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName))
            throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' has no simple name for Unity test data resolution.");

        return Path.Combine(root, assemblyName);
    }

    /// <summary>
    /// Gets the .NET reference-pack metadata references used for semantic analysis.
    /// </summary>
    public static ImmutableArray<MetadataReference> FrameworkReferences
        => CachedFrameworkReferences.Value;

    /// <summary>
    /// Gets metadata references for the Unity runtime and currently loaded test assets.
    /// </summary>
    public static ImmutableArray<MetadataReference> RuntimeReferences
        => CachedRuntimeReferences.Value;

    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        var references = LoadMetadataReferences(
            RequiredEnvironmentVariable(ReferenceRootVariable),
            "Unity test framework reference root");
        return references.Add(MetadataReference.CreateFromFile(
            typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location));
    }

    private static ImmutableArray<MetadataReference> LoadMetadataReferences(
        string root,
        string description)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(
                $"The {description} '{root}' does not exist.");

        var paths = Directory
            .EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
            throw new InvalidOperationException(
                $"The {description} '{root}' contains no reference assemblies.");

        return paths
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> LoadRuntimeReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                continue;

            var fileName = Path.GetFileName(assembly.Location);
            if (IsRuntimeAssembly(fileName) || IsJsonAssembly(fileName) || IsUnityAssembly(fileName))
                paths.Add(assembly.Location);
        }

        AddAssemblyPath(paths, typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly);

        if (paths.Count == 0)
            throw new InvalidOperationException(
                "Unity did not expose any runtime metadata assemblies to the test host.");

        return paths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static bool IsRuntimeAssembly(string fileName)
        => fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("System.dll", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);

    private static bool IsJsonAssembly(string fileName)
        => fileName.Equals("Newtonsoft.Json.dll", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("System.Text.Json.dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnityAssembly(string fileName)
        => fileName.Equals("UnityEditor.dll", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("UnityEngine.", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase);

    private static void AddAssemblyPath(HashSet<string> paths, Assembly assembly)
    {
        if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            paths.Add(assembly.Location);
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Unity test host environment variable '{name}' is not set.");

        return Path.GetFullPath(value);
    }
}

#endif
