using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public abstract class BehavioralTestBase
{
    private static readonly ConcurrentDictionary<string, Assembly> CompilationCache = new();

    private static readonly MetadataReference[] BaseReferences = GetBaseReferences();

    private static MetadataReference[] GetBaseReferences()
    {
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        // Houtamelo.Spire.Core — provides [EnforceInitialization] and other marker attributes
        refs.Add(MetadataReference.CreateFromFile(
            typeof(EnforceInitializationAttribute).Assembly.Location));

        // Newtonsoft.Json — not a platform assembly, needed for NSJ converter compilation
        refs.Add(MetadataReference.CreateFromFile(
            typeof(Newtonsoft.Json.JsonConvert).Assembly.Location));

        return refs.ToArray();
    }

    /// Compiles source through the generator, emits to in-memory assembly, returns loaded Assembly.
    protected static Assembly CompileAndLoad(string source)
    {
        return CompilationCache.GetOrAdd(source, static src =>
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(src,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: "test.cs");

            var compilation = CSharpCompilation.Create(
                $"Behavioral_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                BaseReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true));

            var generator = new DiscriminatedUnionGenerator();
            CSharpGeneratorDriver.Create(generator)
                .RunGeneratorsAndUpdateCompilation(
                    compilation,
                    out var outputCompilation,
                    out var generatorDiags);

            var warnings = generatorDiags
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .ToList();
            if (warnings.Count > 0)
            {
                var msgs = string.Join("\n", warnings.Select(d => $"  {d.Id}: {d.GetMessage()}"));
                Assert.Fail($"Generator reported {warnings.Count} diagnostic(s):\n{msgs}");
            }

            var errors = outputCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();
            if (errors.Count > 0)
            {
                var msgs = string.Join("\n",
                    errors.Select(e => $"  {e.Location}: {e.Id}: {e.GetMessage()}"));
                Assert.Fail($"Compilation has {errors.Count} error(s):\n{msgs}");
            }

            using var ms = new MemoryStream();
            EmitResult emitResult = outputCompilation.Emit(ms);
            if (!emitResult.Success)
            {
                var emitErrors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"  {d.Id}: {d.GetMessage()}");
                Assert.Fail($"Emit failed:\n{string.Join("\n", emitErrors)}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        });
    }

    protected static Type GetType(Assembly asm, string fullName)
        => asm.GetType(fullName, throwOnError: true)!;

    /// Invoke a static factory method on the union type.
    protected static object InvokeFactory(Type unionType, string variantName, params object[] args)
    {
        var method = unionType.GetMethod(variantName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, args)!;
    }

    /// Read a public instance property.
    protected static object? ReadProperty(object instance, string propName)
    {
        var prop = instance.GetType().GetProperty(propName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        return prop.GetValue(instance);
    }

    /// Read the public `kind` property, returning the underlying integral value.
    protected static int ReadKind(object instance)
    {
        var prop = instance.GetType().GetProperty("kind",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        return Convert.ToInt32(prop.GetValue(instance)!);
    }

    /// Get a Kind enum ordinal by variant name (e.g. "Circle" → 0).
    protected static int GetKindValue(Type unionType, string variantName)
    {
        var kindType = unionType.GetNestedType("Kind");
        Assert.NotNull(kindType);
        var field = kindType.GetField(variantName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        return Convert.ToInt32(field.GetRawConstantValue()!);
    }

    /// Invoke a static method on a type by name.
    protected static object? InvokeMethod(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, args);
    }

    /// Invoke Deconstruct with the given parameter count, returning all out values.
    protected static object?[] InvokeDeconstruct(object instance, int paramCount)
    {
        var type = instance.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Deconstruct"
                && m.GetParameters().Length == paramCount);
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        var args = new object?[paramCount];
        for (int i = 0; i < paramCount; i++)
        {
            // out parameters need default-initialized values
            var elementType = parameters[i].ParameterType.GetElementType()!;
            args[i] = elementType.IsValueType
                ? Activator.CreateInstance(elementType)
                : null;
        }

        method.Invoke(instance, args);
        return args;
    }

    // ── Strategy enumerations ───────────────────────────────────────

    public static IEnumerable<object[]> StructStrategies => new[]
    {
        new object[] { "Layout.Additive" },
        new object[] { "Layout.Overlap" },
        new object[] { "Layout.BoxedFields" },
        new object[] { "Layout.BoxedTuple" },
        new object[] { "Layout.UnsafeOverlap" },
    };

    public static IEnumerable<object[]> GenericCapableStrategies => new[]
    {
        new object[] { "Layout.Additive" },
        new object[] { "Layout.BoxedFields" },
        new object[] { "Layout.BoxedTuple" },
    };

    // ── Source templates ────────────────────────────────────────────

    protected static string BasicShapeSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Square(int sideLength);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }
        """;

    protected static string SharedFieldsSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Vec
        {
            [Variant] public static partial Vec Vec2(double x, double y);
            [Variant] public static partial Vec Vec3(double x, double y, double z);
        }
        """;

    protected static string RefFieldsSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Message
        {
            [Variant] public static partial Message Label(string text);
            [Variant] public static partial Message ColoredLine(int x, int y, string color);
            [Variant] public static partial Message Empty();
        }
        """;

    protected static string GenericOptionSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Option<T>
        {
            [Variant] public static partial Option<T> Some(T value);
            [Variant] public static partial Option<T> None();
        }
        """;

    protected static string LargeUnionSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Event
        {
            [Variant] public static partial Event Point();
            [Variant] public static partial Event Circle(double radius);
            [Variant] public static partial Event Label(string text);
            [Variant] public static partial Event Rectangle(float width, float height);
            [Variant] public static partial Event ColoredLine(int x1, int y1, string color);
            [Variant] public static partial Event Transform(float x, float y, float z, float w);
            [Variant] public static partial Event RichText(string text, int size, bool bold, string font, double spacing);
            [Variant] public static partial Event Error(string message);
        }
        """;

    protected static string PatternMatchingSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}})]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Square(int sideLength);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }

        public static class TestRunner
        {
            public static string MatchKind(Shape s) => s.kind switch
            {
                Shape.Kind.Circle => "circle",
                Shape.Kind.Square => "square",
                Shape.Kind.Rectangle => "rectangle",
                Shape.Kind.Point => "point",
                _ => "unknown"
            };

            public static double ExtractRadius(Shape s) => s switch
            {
                { kind: Shape.Kind.Circle, radius: var r } => r,
                _ => -1.0
            };

            public static string ExtractMultiField(Shape s) => s switch
            {
                { kind: Shape.Kind.Rectangle, width: var w, height: var h } => $"{w},{h}",
                _ => "none"
            };
        }
        """;

    protected static string JsonStjSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}}, Json = JsonLibrary.SystemTextJson)]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }
        """;

    protected static string JsonNsjSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}}, Json = JsonLibrary.NewtonsoftJson)]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }
        """;

    protected static string JsonStjCustomDiscriminatorSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}}, Json = JsonLibrary.SystemTextJson, JsonDiscriminator = "type")]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Point();
        }
        """;

    protected static string JsonStjJsonNameSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}}, Json = JsonLibrary.SystemTextJson)]
        public partial struct Shape
        {
            [Variant, JsonName("circle")]
            public static partial Shape Circle([JsonName("r")] double radius);
            [Variant, JsonName("pt")]
            public static partial Shape Point();
        }
        """;

    protected static string JsonStjGenericSource(string strategy) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion({{strategy}}, Json = JsonLibrary.SystemTextJson)]
        public partial struct Option<T>
        {
            [Variant] public static partial Option<T> Some(T value);
            [Variant] public static partial Option<T> None();
        }
        """;

    protected const string JsonStjRecordSource = """
        using Houtamelo.Spire;
        namespace TestNs
        {
            [DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
            public abstract partial record Shape
            {
                public sealed partial record Circle(double Radius) : Shape;
                public sealed partial record Square(int Side) : Shape;
                public sealed partial record Point() : Shape;
            }
        }
        """;

    protected const string JsonNsjRecordSource = """
        using Houtamelo.Spire;
        namespace TestNs
        {
            [DiscriminatedUnion(Json = JsonLibrary.NewtonsoftJson)]
            public abstract partial record Shape
            {
                public sealed partial record Circle(double Radius) : Shape;
                public sealed partial record Square(int Side) : Shape;
                public sealed partial record Point() : Shape;
            }
        }
        """;

    protected const string RecordUnionSource = """
        using Houtamelo.Spire;
        namespace TestNs
        {
            [DiscriminatedUnion]
            public abstract partial record Shape
            {
                public sealed partial record Circle(double Radius) : Shape;
                public sealed partial record Square(int Side) : Shape;
                public sealed partial record Point() : Shape;
            }

            public static class TestRunner
            {
                public static string TypeMatch(Shape s) => s switch
                {
                    Shape.Circle c => $"circle:{c.Radius}",
                    Shape.Square sq => $"square:{sq.Side}",
                    Shape.Point => "point",
                    _ => "unknown"
                };
            }
        }
        """;

    protected const string ClassUnionSource = """
        using Houtamelo.Spire;
        namespace TestNs
        {
            [DiscriminatedUnion]
            public abstract partial class Command
            {
                public sealed partial class Start : Command { }
                public sealed partial class Stop : Command
                {
                    public string Reason { get; }
                    public Stop(string reason) { Reason = reason; }
                }
            }

            public static class TestRunner
            {
                public static string TypeMatch(Command c) => c switch
                {
                    Command.Start => "start",
                    Command.Stop s => $"stop:{s.Reason}",
                    _ => "unknown"
                };
            }
        }
        """;
}
