using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public sealed class OverlapConstructorTests : BehavioralTestBase
{
    private static readonly MetadataReference CoreReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> NoUnsafeReferences =
        new(ResolveNoUnsafeReferencesAsync);

    [Fact]
    public async Task Factories_PreservePrimitiveValuesAndKindsWithoutUnsafe()
    {
        var result = await GenerateAsync(BasicSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);
        var unionType = GetType(assembly, "OverlapConstructorBasic");

        var circle = InvokeFactory(unionType, "Circle", 3.25d);
        Assert.Equal(GetKindValue(unionType, "Circle"), ReadKind(circle));
        Assert.Equal(3.25d, ReadProperty(circle, "radius"));

        var rectangle = InvokeFactory(unionType, "Rectangle", 4.5f, 8.25f);
        Assert.Equal(GetKindValue(unionType, "Rectangle"), ReadKind(rectangle));
        Assert.Equal(4.5f, ReadProperty(rectangle, "width"));
        Assert.Equal(8.25f, ReadProperty(rectangle, "height"));

        var rectangleCopy = RuntimeHelpers.GetObjectValue(rectangle);
        _ = InvokeFactory(unionType, "Circle", 7.5d);
        Assert.Equal(4.5f, ReadProperty(rectangleCopy, "width"));
        Assert.Equal(8.25f, ReadProperty(rectangleCopy, "height"));

        var point = InvokeFactory(unionType, "Point");
        Assert.Equal(GetKindValue(unionType, "Point"), ReadKind(point));
        Assert.Equal(0d, ReadProperty(point, "radius"));
        Assert.Equal(0f, ReadProperty(point, "width"));
        Assert.Equal(0f, ReadProperty(point, "height"));
        Assert.DoesNotContain("Unsafe", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => string.Equals(
                reference.Name,
                "System.Runtime.CompilerServices.Unsafe",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Factories_MapSameSignatureFieldsAndReorderedArguments()
    {
        var result = await GenerateAsync(ParameterMappingSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);
        var unionType = GetType(assembly, "OverlapConstructorParameterMapping");

        var first = InvokeFactory(unionType, "First", 11, 22);
        Assert.Equal(11, ReadProperty(first, "x"));
        Assert.Equal(22, ReadProperty(first, "y"));
        Assert.Equal(0, ReadProperty(first, "left"));
        Assert.Equal(0, ReadProperty(first, "right"));

        var second = InvokeFactory(unionType, "Second", 33, 44);
        Assert.Equal(33, ReadProperty(second, "left"));
        Assert.Equal(44, ReadProperty(second, "right"));
        Assert.Equal(0, ReadProperty(second, "x"));
        Assert.Equal(0, ReadProperty(second, "y"));

        var forward = InvokeFactory(unionType, "Forward", 55, "forward");
        Assert.Equal(55, ReadProperty(forward, "number"));
        Assert.Equal("forward", ReadProperty(forward, "text"));

        var reversed = InvokeFactory(unionType, "Reversed", "reversed", 66);
        Assert.Equal("reversed", ReadProperty(reversed, "text"));
        Assert.Equal(66, ReadProperty(reversed, "number"));
    }

    [Fact]
    public async Task Factories_PreserveReferencesNullabilityAndNestedGenericIdentity()
    {
        var result = await GenerateAsync(ReferenceAndNullableSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);

        var referencesType = GetType(assembly, "OverlapConstructorReferences");
        var text = new string(new[] { 't', 'e', 'x', 't' });
        var textValue = InvokeFactory(referencesType, "Text", text);
        Assert.Same(text, ReadProperty(textValue, "value"));

        var detail = new object();
        var objectValue = InvokeFactory(referencesType, "Object", detail);
        var objectDetailProperty = FindProperty(referencesType, "detail", "object_detail");
        Assert.Same(detail, objectDetailProperty.GetValue(objectValue));

        var dynamicValue = new object();
        var dynamicUnion = InvokeFactory(referencesType, "Dynamic", dynamicValue);
        var dynamicDetailProperty = FindProperty(referencesType, "detail", "dynamic_detail");
        Assert.Same(dynamicValue, dynamicDetailProperty.GetValue(dynamicUnion));
        Assert.Null(ReadProperty(InvokeFactory(referencesType, "Text", (object)null!), "value"));
        Assert.Null(objectDetailProperty.GetValue(InvokeFactory(referencesType, "Object", (object)null!)));
        var emptyReferences = InvokeFactory(referencesType, "Empty");
        Assert.Null(ReadProperty(emptyReferences, "value"));
        Assert.Null(objectDetailProperty.GetValue(emptyReferences));

        var nullableType = GetType(assembly, "OverlapConstructorNullableFields");
        var required = InvokeFactory(nullableType, "Required", "required");
        Assert.Equal("required", ReadProperty(required, "required_value"));
        var optional = InvokeFactory(nullableType, "Optional", (object)null!);
        Assert.Null(ReadProperty(optional, "optional_value"));

        var maybe = InvokeFactory(nullableType, "Maybe", (int?)73);
        var maybeProperty = nullableType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.PropertyType == typeof(int?));
        Assert.Equal((int?)73, maybeProperty.GetValue(maybe));
        var maybeNull = InvokeFactory(nullableType, "Maybe", (object)null!);
        Assert.Null(maybeProperty.GetValue(maybeNull));

        var values = new List<string> { "nested" };
        var listValue = InvokeFactory(nullableType, "RequiredList", values);
        Assert.Same(values, ReadProperty(listValue, "requiredValues"));
        var nullableListValue = InvokeFactory(nullableType, "OptionalList", values);
        Assert.Same(values, ReadProperty(nullableListValue, "optionalValues"));
        var sharedListValue = InvokeFactory(nullableType, "SharedRequiredList", values);
        Assert.Same(values, ReadProperty(sharedListValue, "sharedValues"));
        var sharedNullableListValue = InvokeFactory(nullableType, "SharedOptionalList", values);
        Assert.Same(values, ReadProperty(sharedNullableListValue, "sharedValues"));

        var tupleFirst = InvokeFactory(nullableType, "TupleFirst", (1, "first"));
        var firstTuple = ReadProperty(tupleFirst, "firstTuple")!;
        Assert.Equal(1, firstTuple.GetType().GetField("Item1")!.GetValue(firstTuple));
        Assert.Equal("first", firstTuple.GetType().GetField("Item2")!.GetValue(firstTuple));
        var tupleSecond = InvokeFactory(nullableType, "TupleSecond", (2, (string)null!));
        var secondTuple = ReadProperty(tupleSecond, "secondTuple")!;
        Assert.Equal(2, secondTuple.GetType().GetField("Item1")!.GetValue(secondTuple));
        Assert.Null(secondTuple.GetType().GetField("Item2")!.GetValue(secondTuple));
    }

    [Fact]
    public async Task Factories_PreserveBoxedValuesAndTupleElementValues()
    {
        var result = await GenerateAsync(BoxedAndTupleSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);

        var pointType = GetType(assembly, "Point2");
        var point = Activator.CreateInstance(pointType, 1, 2)!;
        var otherPoint = Activator.CreateInstance(pointType, 3, 4)!;
        var boxedType = GetType(assembly, "OverlapConstructorBoxedValue");

        var dot = InvokeFactory(boxedType, "Dot", point);
        Assert.Equal(point, ReadProperty(dot, "location"));
        var pair = InvokeFactory(boxedType, "Pair", point, otherPoint);
        Assert.Equal(point, ReadProperty(pair, "first"));
        Assert.Equal(otherPoint, ReadProperty(pair, "second"));

        var boxedTuple = InvokeFactory(boxedType, "Coordinates", (5, 6));
        var coordinates = ReadProperty(boxedTuple, "coordinates")!;
        Assert.Equal(5, coordinates.GetType().GetField("Item1")!.GetValue(coordinates));
        Assert.Equal(6, coordinates.GetType().GetField("Item2")!.GetValue(coordinates));

        var outerIntType = GetType(assembly, "Outer`1").MakeGenericType(typeof(int));
        var outerStringType = GetType(assembly, "Outer`1").MakeGenericType(typeof(string));
        var intNestedType = outerIntType.GetNestedType("Nested")!.MakeGenericType(typeof(int));
        var stringNestedType = outerStringType.GetNestedType("Nested")!.MakeGenericType(typeof(string));
        var intNested = Activator.CreateInstance(intNestedType, 7)!;
        var stringNested = Activator.CreateInstance(stringNestedType, "nested")!;
        var intNestedValue = InvokeFactory(boxedType, "IntNested", intNested);
        var stringNestedValue = InvokeFactory(boxedType, "StringNested", stringNested);
        var intNestedProperty = boxedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.PropertyType == intNestedType);
        var stringNestedProperty = boxedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.PropertyType == stringNestedType);
        Assert.Equal(intNested, intNestedProperty.GetValue(intNestedValue));
        Assert.Equal(stringNested, stringNestedProperty.GetValue(stringNestedValue));

        var eightTuple = (1, 2, 3, 4, 5, 6, 7, 8);
        var longTupleValue = InvokeFactory(boxedType, "LongTuple", eightTuple);
        var explicitTupleValue = InvokeFactory(boxedType, "ExplicitTuple", eightTuple);
        var longTuple = ReadProperty(longTupleValue, "longTuple")!;
        var explicitTuple = ReadProperty(explicitTupleValue, "explicitTuple")!;
        Assert.Equal(1, longTuple.GetType().GetField("Item1")!.GetValue(longTuple));
        Assert.Equal(8, longTuple.GetType().GetField("Rest")!.GetValue(longTuple)!.GetType()
            .GetField("Item1")!.GetValue(longTuple.GetType().GetField("Rest")!.GetValue(longTuple)));
        Assert.Equal(1, explicitTuple.GetType().GetField("Item1")!.GetValue(explicitTuple));
        Assert.Equal(8, explicitTuple.GetType().GetField("Rest")!.GetValue(explicitTuple)!.GetType()
            .GetField("Item1")!.GetValue(explicitTuple.GetType().GetField("Rest")!.GetValue(explicitTuple)));

        var tupleType = GetType(assembly, "OverlapConstructorTuple");
        var tuple = (1, 2);
        var first = InvokeFactory(tupleType, "First", tuple);
        var firstPoint = ReadProperty(first, "point")!;
        Assert.Equal(1, firstPoint.GetType().GetField("Item1")!.GetValue(firstPoint));
        Assert.Equal(2, firstPoint.GetType().GetField("Item2")!.GetValue(firstPoint));

        var second = InvokeFactory(tupleType, "Second", (3, 4));
        var secondPoint = ReadProperty(second, "point")!;
        Assert.Equal(3, secondPoint.GetType().GetField("Item1")!.GetValue(secondPoint));
        Assert.Equal(4, secondPoint.GetType().GetField("Item2")!.GetValue(secondPoint));
    }

    [Fact]
    public async Task ReadonlyFactories_PreserveStorageAndPublicApi()
    {
        var result = await GenerateAsync(ReadonlySource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);
        var unionType = GetType(assembly, "OverlapConstructorReadonly");

        Assert.Equal(LayoutKind.Explicit, unionType.StructLayoutAttribute!.Value);
        var fields = unionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(
            new[] { "_kind", "_number", "_text" },
            fields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(fields, field => Assert.True(field.IsInitOnly, field.Name + " must remain readonly."));
        Assert.Equal(0, GetFieldOffset(fields.Single(field => field.Name == "_kind")));

        var number = InvokeFactory(unionType, "Number", 99);
        Assert.Equal(99, ReadProperty(number, "number"));
        var text = InvokeFactory(unionType, "Text", "readonly");
        Assert.Equal("readonly", ReadProperty(text, "text"));
        Assert.NotNull(unionType.GetMethod("Number", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(unionType.GetProperty("number", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public async Task NestedAndFieldlessFactoriesRemainConstructible()
    {
        var result = await GenerateAsync(NestedAndFieldlessSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);

        var nestedType = GetType(assembly, "OverlapConstructorOuter+Nested");
        var nestedNumber = InvokeFactory(nestedType, "Number", 12);
        Assert.Equal(GetKindValue(nestedType, "Number"), ReadKind(nestedNumber));
        Assert.Equal(12, ReadProperty(nestedNumber, "number"));
        var nestedEmpty = InvokeFactory(nestedType, "Empty");
        Assert.Equal(GetKindValue(nestedType, "Empty"), ReadKind(nestedEmpty));

        var fieldlessType = GetType(assembly, "OverlapConstructorFieldless");
        var fieldlessFields = fieldlessType.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(new[] { "_kind" }, fieldlessFields.Select(field => field.Name));
        foreach (var variant in new[] { "None", "Done", "Cancelled" })
        {
            var value = InvokeFactory(fieldlessType, variant);
            Assert.Equal(GetKindValue(fieldlessType, variant), ReadKind(value));
        }
    }

    [Fact]
    public async Task GeneratedFields_RetainOffsetsReadonlyModifiersAndApiShape()
    {
        var result = await GenerateAsync(BasicSource, useNoUnsafeReferences: true);
        var assembly = EmitAndLoad(result);
        var unionType = GetType(assembly, "OverlapConstructorBasic");

        var fields = unionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(
            new[] { "_height", "_kind", "_radius", "_width" },
            fields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(fields, field => Assert.True(field.IsInitOnly, field.Name + " must remain readonly."));
        Assert.All(fields, field => Assert.NotNull(field.GetCustomAttribute<FieldOffsetAttribute>()));
        Assert.Equal(0, GetFieldOffset(fields.Single(field => field.Name == "_kind")));

        var circle = unionType.GetMethod("Circle", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(circle);
        Assert.Equal(new[] { typeof(double) }, circle!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.NotNull(unionType.GetProperty("kind", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("IsCircle", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("IsRectangle", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("radius", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("width", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(unionType.GetProperty("height", BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(
            unionType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "Deconstruct" && method.GetParameters().Length == 2);
        Assert.Contains(
            unionType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "Deconstruct" && method.GetParameters().Length == 3);
    }

    [Fact]
    public async Task ModernHost_PreservesFactoriesDeconstructsAndInitProperties()
    {
        var result = await GenerateAsync(BasicSource, useNoUnsafeReferences: false);
        var assembly = EmitAndLoad(result);
        var unionType = GetType(assembly, "OverlapConstructorBasic");

        var rectangle = InvokeFactory(unionType, "Rectangle", 1.25f, 2.5f);
        Assert.Equal(1.25f, ReadProperty(rectangle, "width"));
        Assert.Equal(2.5f, ReadProperty(rectangle, "height"));
        var deconstructed = InvokeDeconstruct(rectangle, 3);
        Assert.Equal(GetKindValue(unionType, "Rectangle"), Convert.ToInt32(deconstructed[0]));
        Assert.Equal(1.25f, deconstructed[1]);
        Assert.Equal(2.5f, deconstructed[2]);
        Assert.Contains("init", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadonlyFieldAssignmentControl_RemainsCompilerError()
    {
        const string source = """
            public readonly struct ReadonlyAssignmentControl
            {
                private readonly int _value;

                public static ReadonlyAssignmentControl Create(int value)
                {
                    var result = new ReadonlyAssignmentControl();
                    result._value = value;
                    return result;
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9));
        var compilation = CSharpCompilation.Create(
            "ReadonlyAssignmentControl",
            new[] { tree },
            TestHarnessEnvironment.FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Contains(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && (diagnostic.Id == "CS0191" || diagnostic.Id == "CS8331"));
    }

    private static async Task<GenerationResult> GenerateAsync(
        string source,
        bool useNoUnsafeReferences)
    {
        var references = useNoUnsafeReferences
            ? await NoUnsafeReferences.Value
            : TestHarnessEnvironment.RuntimeReferences.Add(CoreReference);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(
            useNoUnsafeReferences ? LanguageVersion.CSharp9 : LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "overlap-constructor.cs");
        var compilation = CSharpCompilation.Create(
            "OverlapConstructor_" + Guid.NewGuid().ToString("N"),
            CreateSyntaxTrees(tree, parseOptions, references, useNoUnsafeReferences),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        if (useNoUnsafeReferences)
        {
            var unsafeType = compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.Unsafe");
#if SPIRE_UNITY_TEST_HOST
            Assert.True(
                unsafeType is null || !compilation.IsSymbolAccessibleWithin(unsafeType, compilation.Assembly),
                "The Unity host compilation must not expose an accessible Unsafe definition.");
#else
            Assert.Null(unsafeType);
#endif
            var initType = compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.IsExternalInit");
            Assert.True(
                initType is null || !compilation.IsSymbolAccessibleWithin(initType, compilation.Assembly),
                "The old-host compilation must not expose an accessible IsExternalInit definition.");
        }

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

        var generatedSource = string.Join(
            "\n",
            driver.GetRunResult().GeneratedTrees.Select(treeItem => treeItem.GetText().ToString()));
        Assert.DoesNotContain("Unsafe", generatedSource, StringComparison.Ordinal);

        var outputDiagnostics = outputCompilation.GetDiagnostics().ToArray();
        var generatedTreePaths = driver.GetRunResult().GeneratedTrees
            .Select(treeItem => treeItem.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);
        var generatedNullableWarnings = outputDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning
                && diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal)
                && diagnostic.Location.SourceTree is not null
                && generatedTreePaths.Contains(diagnostic.Location.SourceTree.FilePath));
        Assert.Empty(generatedNullableWarnings);

        var generatorProblems = generatorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        var compilationErrors = outputDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        var problems = generatorProblems
            .Cast<Microsoft.CodeAnalysis.Diagnostic>()
            .Concat(compilationErrors)
            .ToArray();
        Assert.True(
            problems.Length == 0,
            "Generation produced diagnostics:\n" + string.Join(
                "\n",
                problems.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
        return new GenerationResult(outputCompilation, generatedSource);
    }

    private static Assembly EmitAndLoad(GenerationResult result)
    {
        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(
            emitResult.Success,
            string.Join("\n", emitResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private static int GetFieldOffset(FieldInfo field)
    {
        var attribute = field.GetCustomAttribute<FieldOffsetAttribute>();
        Assert.NotNull(attribute);
        return attribute!.Value;
    }

    private static PropertyInfo FindProperty(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
                return property;
        }

        Assert.Fail("None of the expected properties were generated: " + string.Join(", ", names));
        throw new InvalidOperationException();
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveNoUnsafeReferencesAsync()
    {
#if SPIRE_UNITY_TEST_HOST
        await Task.CompletedTask;
        var references = TestHarnessEnvironment.RuntimeReferences;
#else
        var references = await ReferenceAssemblies.NetCore.NetCoreApp31.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);
#endif
#if SPIRE_UNITY_TEST_HOST
        var hostReferences = references
            .Where(reference => !ReferenceDefinesAccessibleType(
                reference,
                "System.Runtime.CompilerServices.Unsafe"))
            .Where(reference => !ReferenceDefinesAccessibleType(
                reference,
                "System.Runtime.CompilerServices.IsExternalInit"))
            .ToImmutableArray();
#else
        var hostReferences = references
            .Where(reference => !ReferenceDefinesType(
                reference,
                "System.Runtime.CompilerServices.Unsafe"))
            .Where(reference => !ReferenceDefinesType(
                reference,
                "System.Runtime.CompilerServices.IsExternalInit"))
            .ToImmutableArray();
#endif

        var coreProbe = CSharpCompilation.Create(
            "NoUnsafeCoreReferenceProbe",
            references: new[] { CoreReference });
        var coreAssembly = coreProbe.GetAssemblyOrModuleSymbol(CoreReference) as IAssemblySymbol;
        var coreInitType = coreAssembly?.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.IsExternalInit");
        Assert.NotNull(coreInitType);
        Assert.False(coreProbe.IsSymbolAccessibleWithin(coreInitType!, coreProbe.Assembly));

        // The old reference pack must provide neither compiler helper. The
        // runtime Spire reference is appended below and intentionally contains
        // its own IsExternalInit compatibility declaration.
        var hostProbe = CSharpCompilation.Create("NoUnsafeReferenceHostProbe", references: hostReferences);
        Assert.NotNull(hostProbe.GetTypeByMetadataName("System.Object"));
        var hostUnsafeType = hostProbe.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.Unsafe");
#if SPIRE_UNITY_TEST_HOST
        Assert.True(
            hostUnsafeType is null || !hostProbe.IsSymbolAccessibleWithin(hostUnsafeType, hostProbe.Assembly),
            "The Unity host reference set must not expose an accessible Unsafe definition.");
#else
        Assert.Null(hostUnsafeType);
#endif
        var hostInitType = hostProbe.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.IsExternalInit");
        Assert.True(
            hostInitType is null || !hostProbe.IsSymbolAccessibleWithin(hostInitType, hostProbe.Assembly),
            "The old reference pack must not expose an accessible IsExternalInit definition.");

        var filtered = hostReferences.Append(CoreReference).ToImmutableArray();
        var probe = CSharpCompilation.Create("NoUnsafeReferenceProbe", references: filtered);
        var filteredUnsafeType = probe.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.Unsafe");
#if SPIRE_UNITY_TEST_HOST
        Assert.True(
            filteredUnsafeType is null || !probe.IsSymbolAccessibleWithin(filteredUnsafeType, probe.Assembly),
            "The Unity no-Unsafe references must not expose an accessible Unsafe definition.");
#else
        Assert.Null(filteredUnsafeType);
#endif
        var initType = probe.GetTypeByMetadataName("System.Runtime.CompilerServices.IsExternalInit");
        Assert.True(
            initType is null || !probe.IsSymbolAccessibleWithin(initType, probe.Assembly),
            "The no-Unsafe references must not expose an accessible IsExternalInit definition.");
        return filtered;
    }

    private static IEnumerable<SyntaxTree> CreateSyntaxTrees(
        SyntaxTree sourceTree,
        CSharpParseOptions parseOptions,
        IEnumerable<MetadataReference> references,
        bool useNoUnsafeReferences)
    {
        yield return sourceTree;
        if (useNoUnsafeReferences)
            yield break;

        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
            parseOptions,
            references);
        if (compatibilityTree is not null)
            yield return compatibilityTree;
    }

    private static bool ReferenceDefinesType(MetadataReference reference, string metadataName)
    {
        var probe = CSharpCompilation.Create(
            "ReferenceProbe",
            references: new[] { reference });
        var assembly = probe.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
        return assembly?.GetTypeByMetadataName(metadataName) is not null;
    }

    private static bool ReferenceDefinesAccessibleType(
        MetadataReference reference,
        string metadataName)
    {
        var probe = CSharpCompilation.Create(
            "AccessibleReferenceProbe",
            references: new[] { reference });
        var assembly = probe.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
        var type = assembly?.GetTypeByMetadataName(metadataName);
        return type is not null && probe.IsSymbolAccessibleWithin(type, probe.Assembly);
    }

    private sealed record GenerationResult(Compilation OutputCompilation, string GeneratedSource);

    private const string BasicSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorBasic
        {
            [Variant] public static partial OverlapConstructorBasic Circle(double radius);
            [Variant] public static partial OverlapConstructorBasic Rectangle(float width, float height);
            [Variant] public static partial OverlapConstructorBasic Point();
        }
        """;

    private const string ParameterMappingSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorParameterMapping
        {
            [Variant] public static partial OverlapConstructorParameterMapping First(int x, int y);
            [Variant] public static partial OverlapConstructorParameterMapping Second(int left, int right);
            [Variant] public static partial OverlapConstructorParameterMapping Forward(int number, string text);
            [Variant] public static partial OverlapConstructorParameterMapping Reversed(string text, int number);
        }
        """;

    private const string ReferenceAndNullableSource = """
        #nullable enable
        using System.Collections.Generic;
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorReferences
        {
            [Variant] public static partial OverlapConstructorReferences Text(string? value);
            [Variant] public static partial OverlapConstructorReferences Object(object detail);
            [Variant] public static partial OverlapConstructorReferences Dynamic(dynamic detail);
            [Variant] public static partial OverlapConstructorReferences Empty();
        }

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorNullableFields
        {
            [Variant] public static partial OverlapConstructorNullableFields Required(string value);
            [Variant] public static partial OverlapConstructorNullableFields Optional(string? value);
            [Variant] public static partial OverlapConstructorNullableFields Maybe(int? value);
            [Variant] public static partial OverlapConstructorNullableFields RequiredList(List<string> requiredValues);
            [Variant] public static partial OverlapConstructorNullableFields OptionalList(List<string?> optionalValues);
            [Variant] public static partial OverlapConstructorNullableFields SharedRequiredList(List<string> sharedValues);
            [Variant] public static partial OverlapConstructorNullableFields SharedOptionalList(List<string?> sharedValues);
            [Variant] public static partial OverlapConstructorNullableFields TupleFirst(
                (int x, string text) firstTuple);
            [Variant] public static partial OverlapConstructorNullableFields TupleSecond(
                (int y, string? caption) secondTuple);
        }
        """;

    private const string BoxedAndTupleSource = """
        #nullable enable
        using Houtamelo.Spire;

        public struct Point2
        {
            public int X;
            public int Y;

            public Point2(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        public struct Outer<T>
        {
            public struct Nested
            {
                public T Value;

                public Nested(T value)
                {
                    Value = value;
                }
            }
        }

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorBoxedValue
        {
            [Variant] public static partial OverlapConstructorBoxedValue Dot(Point2 location);
            [Variant] public static partial OverlapConstructorBoxedValue Pair(Point2 first, Point2 second);
            [Variant] public static partial OverlapConstructorBoxedValue Coordinates((int x, int y) coordinates);
            [Variant] public static partial OverlapConstructorBoxedValue IntNested(Outer<int>.Nested value);
            [Variant] public static partial OverlapConstructorBoxedValue StringNested(Outer<string>.Nested value);
            [Variant] public static partial OverlapConstructorBoxedValue LongTuple(
                (int a, int b, int c, int d, int e, int f, int g, int h) longTuple);
            [Variant] public static partial OverlapConstructorBoxedValue ExplicitTuple(
                System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int>> explicitTuple);
            [Variant] public static partial OverlapConstructorBoxedValue Empty();
        }

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorTuple
        {
            [Variant] public static partial OverlapConstructorTuple First((int x, int y) point);
            [Variant] public static partial OverlapConstructorTuple Second((int left, int right) point);
        }
        """;

    private const string ReadonlySource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.Overlap)]
        public readonly partial struct OverlapConstructorReadonly
        {
            [Variant] public static partial OverlapConstructorReadonly Number(int number);
            [Variant] public static partial OverlapConstructorReadonly Text(string text);
        }
        """;

    private const string NestedAndFieldlessSource = """
        #nullable enable
        using Houtamelo.Spire;

        public partial class OverlapConstructorOuter
        {
            [DiscriminatedUnion(Layout.Overlap)]
            public partial struct Nested
            {
                [Variant] public static partial Nested Number(int number);
                [Variant] public static partial Nested Empty();
            }
        }

        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct OverlapConstructorFieldless
        {
            [Variant] public static partial OverlapConstructorFieldless None();
            [Variant] public static partial OverlapConstructorFieldless Done();
            [Variant] public static partial OverlapConstructorFieldless Cancelled();
        }
        """;
}
