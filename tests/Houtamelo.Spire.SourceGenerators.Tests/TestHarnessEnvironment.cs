using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#if SPIRE_UNITY_TEST_HOST
using Spire.Tests.Hosting;
#endif

namespace Houtamelo.Spire.SourceGenerators.Tests;

/// Shared paths, reference sets, and emitted-assembly loading for the test harness.
internal static class TestHarnessEnvironment
{
#if SPIRE_UNITY_TEST_HOST
    private static readonly object ResolverGate = new();
    private static bool resolverInstalled;
#endif

    public static string DataRoot
    {
        get
        {
#if SPIRE_UNITY_TEST_HOST
            return UnityTestHost.DataRoot(typeof(TestHarnessEnvironment).Assembly);
#else
            return AppContext.BaseDirectory;
#endif
        }
    }

    public static ImmutableArray<MetadataReference> FrameworkReferences
    {
        get
        {
#if SPIRE_UNITY_TEST_HOST
            var references = UnityTestHost.FrameworkReferences;
            var jsonPath = typeof(Newtonsoft.Json.JsonConvert).Assembly.Location;
            if (!references.Any(reference =>
                    string.Equals(reference.Display, jsonPath, StringComparison.OrdinalIgnoreCase)))
            {
                references = references.Add(MetadataReference.CreateFromFile(jsonPath));
            }

            return references;
#else
            return TrustedPlatformReferences();
#endif
        }
    }

    public static ImmutableArray<MetadataReference> RuntimeReferences
    {
        get
        {
#if SPIRE_UNITY_TEST_HOST
            return UnityTestHost.RuntimeReferences;
#else
            return TrustedPlatformReferences();
#endif
        }
    }

    public static Assembly LoadAssembly(byte[] image)
    {
#if SPIRE_UNITY_TEST_HOST
        InstallAssemblyResolver();
#endif
        return Assembly.Load(image);
    }

    /// Returns the runtime-only IsExternalInit compatibility tree when the host
    /// reference set does not expose an accessible definition.
    public static SyntaxTree? CreateRuntimeCompatibilityTree(
        CSharpParseOptions parseOptions,
        IEnumerable<MetadataReference> references)
    {
#if SPIRE_UNITY_TEST_HOST
        var probe = CSharpCompilation.Create(
            "SpireUnityRuntimeCompatibilityProbe",
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    "public sealed class SpireUnityRuntimeCompatibilityProbeType { }",
                    parseOptions,
                    path: "__SpireUnityRuntimeCompatibilityProbe.cs")
            },
            references);
        var initType = probe.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.IsExternalInit");
        if (initType is not null &&
            probe.IsSymbolAccessibleWithin(initType, probe.Assembly))
        {
            return null;
        }

        return CSharpSyntaxTree.ParseText(
            "namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }",
            parseOptions,
            path: "__SpireUnityIsExternalInit.cs");
#else
        return null;
#endif
    }

    private static ImmutableArray<MetadataReference> TrustedPlatformReferences()
    {
        var value = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                "The test process did not provide TRUSTED_PLATFORM_ASSEMBLIES.");

        return value
            .Split(Path.PathSeparator)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

#if SPIRE_UNITY_TEST_HOST
    private static void InstallAssemblyResolver()
    {
        lock (ResolverGate)
        {
            if (resolverInstalled)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            resolverInstalled = true;
        }
    }

    private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name);
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(
                    assembly.GetName().Name,
                    requested.Name,
                    StringComparison.OrdinalIgnoreCase));
        if (loaded is not null)
            return loaded;

        var path = UnityTestHost.RuntimeReferences
            .Select(reference => reference.Display)
            .FirstOrDefault(candidate =>
                candidate is not null &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidate),
                    requested.Name,
                    StringComparison.OrdinalIgnoreCase));
        return path is not null && File.Exists(path)
            ? Assembly.LoadFrom(path)
            : null;
    }
#endif
}
