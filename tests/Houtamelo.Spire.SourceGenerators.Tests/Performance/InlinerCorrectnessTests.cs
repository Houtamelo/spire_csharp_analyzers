using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

public class InlinerCorrectnessTests
{
    [Fact]
    public void QualifiedHints_EncodeAllMetadataIdentity()
    {
        var first = RunInliner(QualifiedHintSource, "qualified-first.cs", out var firstCompilation, out var firstDiagnostics);
        var second = RunInliner(QualifiedHintSource, "qualified-second.cs", out var secondCompilation, out var secondDiagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(firstDiagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(secondDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(firstCompilation);
        GeneratorTestHelper.AssertNoCompilationErrors(secondCompilation);

        var firstSources = InlinerSources(first);
        var secondSources = InlinerSources(second);

        Assert.Equal(6, firstSources.Length);
        Assert.Equal(firstSources.Select(item => item.Hint).OrderBy(value => value),
            secondSources.Select(item => item.Hint).OrderBy(value => value));
        Assert.Equal(firstSources.Length, firstSources.Select(item => item.Hint).Distinct().Count());

        var alpha = firstSources.Single(item => item.Source.Contains("namespace Collision.Alpha", StringComparison.Ordinal));
        var beta = firstSources.Single(item => item.Source.Contains("namespace Collision.Beta", StringComparison.Ordinal));
        Assert.NotEqual(alpha.Hint, beta.Hint);

        var nested = firstSources
            .Where(item => item.Source.Contains("namespace Collision.Nested", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, nested.Length);
        Assert.NotEqual(nested[0].Hint, nested[1].Hint);

        var enclosing = firstSources
            .Where(item => item.Source.Contains("namespace Collision.Arity", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, enclosing.Length);
        Assert.NotEqual(enclosing[0].Hint, enclosing[1].Hint);
    }

    [Fact]
    public void OverloadHints_DoNotDropSourcesOrReportAd0001()
    {
        var result = RunInliner(
            OverloadSource,
            "overloads.cs",
            out _,
            out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");

        var sources = InlinerSources(result);
        Assert.Equal(3, sources.Length);
        Assert.Equal(3, sources.Select(item => item.Hint).Distinct().Count());
    }

    [Fact]
    public void InlinableQualifiedHints_AreDistinctAndDeterministic()
    {
        var first = RunInlinable(InlinableHintSource, "inlinable-qualified-first.cs", out var firstCompilation, out var firstDiagnostics);
        var second = RunInlinable(InlinableHintSource, "inlinable-qualified-second.cs", out var secondCompilation, out var secondDiagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(firstDiagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(secondDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(firstCompilation);
        GeneratorTestHelper.AssertNoCompilationErrors(secondCompilation);

        var firstSources = InlinableSources(first);
        var secondSources = InlinableSources(second);

        Assert.Equal(2, firstSources.Length);
        Assert.Equal(firstSources.Select(item => item.Hint).OrderBy(value => value),
            secondSources.Select(item => item.Hint).OrderBy(value => value));
        Assert.Equal(2, firstSources.Select(item => item.Hint).Distinct().Count());

        var alpha = firstSources.Single(item => item.Source.Contains("namespace Collision.Alpha", StringComparison.Ordinal));
        var beta = firstSources.Single(item => item.Source.Contains("namespace Collision.Beta", StringComparison.Ordinal));
        Assert.NotEqual(alpha.Hint, beta.Hint);
    }

    [Fact]
    public void GeneratedForwarder_EmitsAndExecutes()
    {
        var result = RunInliner(
            ForwarderExecutionSource,
            "forwarder-execution.cs",
            out var compilation,
            out _);

        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        var assembly = Emit(compilation);
        var inlinerType = assembly.GetType("Runtime.Forwarder.Target+IncrementInliner");
        Assert.NotNull(inlinerType);

        var inliner = Activator.CreateInstance(inlinerType!);
        var invoke = inlinerType!.GetMethod("Invoke", new[] { typeof(int) });
        Assert.NotNull(invoke);
        Assert.Equal(8, invoke!.Invoke(inliner, new object[] { 7 }));
    }

    [Fact]
    public void ShadowedConstraintForwarders_EmitAndExecute()
    {
        var result = RunInliner(
            ShadowedConstraintExecutionSource,
            "shadowed-constraint-execution.cs",
            out var compilation,
            out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        var assembly = Emit(compilation);
        var markerType = assembly.GetType("Shadowed.Marker");
        Assert.NotNull(markerType);
        var marker = Activator.CreateInstance(markerType!, new object[] { 17 });

        var containingType = assembly.GetType("Shadowed.Containing`1");
        Assert.NotNull(containingType);
        var containingInliner = containingType!.GetNestedTypes(BindingFlags.Public)
            .Single(type => type.Name.StartsWith("EchoInliner", StringComparison.Ordinal));
        var closedContainingInliner = containingInliner.MakeGenericType(typeof(int), markerType!);
        var containingInvoke = closedContainingInliner.GetMethod("Invoke");
        Assert.NotNull(containingInvoke);
        var containingResult = containingInvoke!.Invoke(
            Activator.CreateInstance(closedContainingInliner),
            new[] { marker });
        Assert.Equal(17, markerType!.GetProperty("Value")!.GetValue(containingResult));

        var nestedType = assembly.GetType("Shadowed.Nested");
        Assert.NotNull(nestedType);
        var nestedInliner = nestedType!.GetNestedTypes(BindingFlags.Public)
            .Single(type => type.Name.StartsWith("EchoInliner", StringComparison.Ordinal));
        var closedNestedInliner = nestedInliner.MakeGenericType(markerType!);
        var nestedInvoke = closedNestedInliner.GetMethod("Invoke");
        Assert.NotNull(nestedInvoke);
        var nestedResult = nestedInvoke!.Invoke(
            Activator.CreateInstance(closedNestedInliner),
            new[] { marker });
        Assert.Equal(17, markerType.GetProperty("Value")!.GetValue(nestedResult));
    }

    [Fact]
    public void GeneratedTwin_EmitsAndExecutes()
    {
        var result = RunInlinable(
            TwinExecutionSource,
            "twin-execution.cs",
            out var compilation,
            out _);

        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        var assembly = Emit(compilation);
        var targetType = assembly.GetType("Runtime.Twin.Target");
        Assert.NotNull(targetType);

        var twin = targetType!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.IsGenericMethodDefinition);
        var closedTwin = twin.MakeGenericMethod(typeof(Increment));
        Assert.Equal(8, closedTwin.Invoke(null, new object[] { default(Increment), 7 }));
    }

    [Fact]
    public void MixedIdentifierSpellingTwins_EmitAndExecute()
    {
        var result = RunInlinable(
            MixedIdentifierSpellingExecutionSource,
            "mixed-identifier-spelling-execution.cs",
            out var compilation,
            out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        var assembly = Emit(compilation);
        var targetType = assembly.GetType("Spelling.Targets");
        Assert.NotNull(targetType);

        var atSign = targetType!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "AtSign" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(Increment));
        var unicodeEscape = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "UnicodeEscape" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(Increment));

        Assert.Equal(8, atSign.Invoke(null, new object[] { default(Increment), 7 }));
        Assert.Equal(8, unicodeEscape.Invoke(null, new object[] { default(Increment), 7 }));
    }

    private static GeneratorDriverRunResult RunInliner(
        string source,
        string path,
        out Compilation compilation,
        out System.Collections.Immutable.ImmutableArray<RoslynDiagnostic> diagnostics)
        => GeneratorTestHelper.RunInlinerStructGenerator(source, out compilation, out diagnostics, path);

    private static GeneratorDriverRunResult RunInlinable(
        string source,
        string path,
        out Compilation compilation,
        out System.Collections.Immutable.ImmutableArray<RoslynDiagnostic> diagnostics)
        => GeneratorTestHelper.RunInlinableTwinGenerator(source, out compilation, out diagnostics, path);

    private static (string Hint, string Source)[] InlinerSources(GeneratorDriverRunResult result)
        => result.GeneratedTrees
            .Where(tree => Path.GetFileName(tree.FilePath).EndsWith(".InlinerStruct.g.cs", StringComparison.Ordinal))
            .Select(tree => (Path.GetFileName(tree.FilePath), tree.GetText().ToString()))
            .OrderBy(item => item.Item1, StringComparer.Ordinal)
            .Select(item => (item.Item1, item.Item2))
            .ToArray();

    private static (string Hint, string Source)[] InlinableSources(GeneratorDriverRunResult result)
        => result.GeneratedTrees
            .Where(tree => Path.GetFileName(tree.FilePath).EndsWith(".InlinableTwin.g.cs", StringComparison.Ordinal))
            .Select(tree => (Path.GetFileName(tree.FilePath), tree.GetText().ToString()))
            .OrderBy(item => item.Item1, StringComparer.Ordinal)
            .Select(item => (item.Item1, item.Item2))
            .ToArray();

    private static Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private readonly struct Increment : IFuncInliner<int, int>
    {
        public int Invoke(int value) => value + 1;
    }

    private const string QualifiedHintSource = """
        using Houtamelo.Spire;

        namespace Collision.Alpha
        {
            public static partial class SameName
            {
                [InlinerStruct]
                public static int Run(int value) => value + 1;
            }
        }

        namespace Collision.Beta
        {
            public static partial class SameName
            {
                [InlinerStruct]
                public static int Run(int value) => value + 2;
            }
        }

        namespace Collision.NestedAlpha
        {
            public partial class Outer
            {
                public partial class Inner
                {
                    [InlinerStruct]
                    public static int Run(int value) => value + 3;
                }
            }
        }

        namespace Collision.NestedBeta
        {
            public partial class Outer
            {
                public partial class Inner
                {
                    [InlinerStruct]
                    public static int Run(int value) => value + 4;
                }
            }
        }

        namespace Collision.Arity
        {
            public partial class Container
            {
                public partial class Inner
                {
                    [InlinerStruct]
                    public static int Run(int value) => value;
                }

                public partial class Inner<T>
                    where T : unmanaged
                {
                    [InlinerStruct]
                    public static T Run(T value) => value;
                }
            }
        }
        """;

    private const string OverloadSource = """
        using Houtamelo.Spire;

        namespace Collision.Overloads
        {
            public static partial class Host
            {
                [InlinerStruct]
                public static int Run(int value) => value;

                [InlinerStruct]
                public static int Run(string value) => value.Length;

                [InlinerStruct]
                public static T Run<T>(T value)
                    where T : unmanaged
                    => value;
            }
        }
        """;

    private const string InlinableHintSource = """
        using System;
        using Houtamelo.Spire;

        namespace Collision.Alpha
        {
            public static partial class Host
            {
                public static int Run([Inlinable] Func<int, int> callback, int value)
                    => callback(value);
            }
        }

        namespace Collision.Beta
        {
            public static partial class Host
            {
                public static int Run([Inlinable] Func<int, int> callback, int value)
                    => callback(value);
            }
        }
        """;

    private const string ForwarderExecutionSource = """
        using Houtamelo.Spire;

        namespace Runtime.Forwarder
        {
            public static partial class Target
            {
                [InlinerStruct]
                public static int Increment(int value) => value + 1;
            }
        }
        """;

    private const string ShadowedConstraintExecutionSource = """
        using Houtamelo.Spire;

        namespace Shadowed
        {
            public interface IMarker { }

            public readonly struct Marker : global::Shadowed.IMarker
            {
                public Marker(int value) => Value = value;

                public int Value { get; }
            }

            public partial class Containing<IMarker>
            {
                [InlinerStruct]
                public static T Echo<T>(T value)
                    where T : global::Shadowed.IMarker
                    => value;
            }

            public partial class Nested
            {
                public interface IMarker { }

                [InlinerStruct]
                public static T Echo<T>(T value)
                    where T : global::Shadowed.IMarker
                    => value;
            }
        }
        """;

    private const string MixedIdentifierSpellingExecutionSource = """
        using System;
        using Houtamelo.Spire;

        namespace Spelling
        {
            public static partial class Targets
            {
                public static int AtSign([Inlinable] Func<int, int> @callback, int value)
                    => callback(value);

                public static int UnicodeEscape([Inlinable] Func<int, int> \u0063allback, int value)
                    => callback(value);
            }
        }
        """;

    private const string TwinExecutionSource = """
        using System;
        using Houtamelo.Spire;

        namespace Runtime.Twin
        {
            public static partial class Target
            {
                public static int Apply([Inlinable] Func<int, int> callback, int value)
                    => callback(value);
            }
        }
        """;
}
