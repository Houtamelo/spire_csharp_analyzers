using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spire.SourceGenerators.Attributes;
using Spire.SourceGenerators.Emit;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators;

/// Generates benchmark type implementations and benchmark classes from a [BenchmarkUnion] declaration.
/// Emits complete DU implementations for each layout strategy (calls the same emitters as DiscriminatedUnionGenerator).
[Generator(LanguageNames.CSharp)]
public sealed class BenchmarkUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(BenchmarkAttributeSource.Hint, BenchmarkAttributeSource.Source));

        var benchmarks = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Houtamelo.Spire.BenchmarkUnionAttribute",
            predicate: static (node, _) => node is StructDeclarationSyntax,
            transform: static (ctx, ct) => ParseBenchmarkUnion(ctx)
        ).Where(static b => b is not null);

        var compilationInfo = context.CompilationProvider.Select(static (comp, _) =>
            new CompilationInfo(
                HasSystemTextJson: comp.GetTypeByMetadataName(
                    "System.Text.Json.Serialization.JsonConverter`1") is not null,
                HasNewtonsoftJson: comp.GetTypeByMetadataName(
                    "Newtonsoft.Json.JsonConverter") is not null,
                AllowsUnsafe: ((CSharpCompilationOptions)comp.Options).AllowUnsafe,
                HasInlineArray: comp.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.InlineArrayAttribute") is not null,
                HasInitProperties: comp.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.IsExternalInit") is not null));

        var combined = benchmarks.Combine(compilationInfo);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (bench, compInfo) = pair;
            if (bench is null) return;
            var b = bench.Value;

            // Emit implementation for the input struct itself (Additive by default)
            {
                var inputUnion = MakeUnionDeclaration(b, b.InputTypeName, "struct", EmitStrategy.Additive, hasInit: compInfo.HasInitProperties);
                ctx.AddSource($"{b.InputTypeName}.Bench.g.cs", Emit(inputUnion));
            }

            // Determine layouts
            var layouts = b.IsGeneric
                ? new[] { EmitStrategy.Additive, EmitStrategy.BoxedFields, EmitStrategy.BoxedTuple }
                : new[] { EmitStrategy.Additive, EmitStrategy.BoxedFields, EmitStrategy.BoxedTuple, EmitStrategy.Overlap, EmitStrategy.UnsafeOverlap };

            // Compute JSON flags
            var jsonFlags = JsonLibrary.None;
            if (compInfo.HasSystemTextJson) jsonFlags |= JsonLibrary.SystemTextJson;
            if (compInfo.HasNewtonsoftJson) jsonFlags |= JsonLibrary.NewtonsoftJson;

            // Emit defining declarations (partial method signatures) + implementing declarations (emitter output)
            foreach (var strategy in layouts)
            {
                var typeName = b.Prefix + StrategyName(strategy);
                var union = MakeUnionDeclaration(b, typeName, "struct", strategy, jsonFlags, compInfo.HasInitProperties);

                // Defining declaration: partial struct with [Variant] method signatures
                ctx.AddSource($"{typeName}.BenchDecl.g.cs", EmitStructDeclaration(b, typeName));

                string source;
                if (strategy == EmitStrategy.UnsafeOverlap)
                {
                    if (!compInfo.AllowsUnsafe) continue;
                    source = UnsafeOverlapEmitter.Emit(union, compInfo.HasInlineArray);
                }
                else
                {
                    source = Emit(union);
                }
                ctx.AddSource($"{typeName}.Bench.g.cs", source);

                // JSON converters (require public properties for field access)
                if (!b.HasFieldNameConflicts)
                {
                    if ((jsonFlags & JsonLibrary.SystemTextJson) != 0)
                        ctx.AddSource($"{typeName}.Stj.g.cs", SystemTextJsonEmitter.Emit(union));
                    if ((jsonFlags & JsonLibrary.NewtonsoftJson) != 0)
                        ctx.AddSource($"{typeName}.Nsj.g.cs", NewtonsoftJsonEmitter.Emit(union));
                }
            }

            // Emit record (declaration + implementation)
            {
                var typeName = b.Prefix + "Record";
                ctx.AddSource($"{typeName}.BenchDecl.g.cs", EmitRecordDeclaration(b, typeName));
                var union = MakeUnionDeclaration(b, typeName, "record", EmitStrategy.Record, jsonFlags, compInfo.HasInitProperties);
                ctx.AddSource($"{typeName}.Bench.g.cs", RecordEmitter.Emit(union));
                if ((jsonFlags & JsonLibrary.SystemTextJson) != 0)
                    ctx.AddSource($"{typeName}.Stj.g.cs", SystemTextJsonEmitter.Emit(union));
                if ((jsonFlags & JsonLibrary.NewtonsoftJson) != 0)
                    ctx.AddSource($"{typeName}.Nsj.g.cs", NewtonsoftJsonEmitter.Emit(union));
            }

            // Emit benchmark classes
            if (!b.IsGeneric)
                ctx.AddSource($"{b.Prefix}.BenchCode.g.cs", EmitBenchmarkCode(b, layouts, compInfo));
        });
    }

    #region Parsing

    record struct BenchField(string Name, string TypeFullName, bool IsUnmanaged, bool IsReferenceType, int? KnownSize);
    record struct BenchVariant(string Name, BenchField[] Fields);

    record struct BenchmarkDef(
        string Namespace,
        string InputTypeName,
        string Prefix,
        bool IsGeneric,
        bool HasFieldNameConflicts,
        string[] TypeParams,
        BenchVariant[] Variants);

    static BenchmarkDef? ParseBenchmarkUnion(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;

        string? prefix = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "BenchmarkUnionAttribute") continue;
            foreach (var named in attr.NamedArguments)
                if (named.Key == "Name" && named.Value.Value is string s)
                    prefix = s;
        }
        prefix ??= symbol.Name;

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? "" : symbol.ContainingNamespace.ToDisplayString();
        var isGeneric = symbol.TypeParameters.Length > 0;
        var typeParams = symbol.TypeParameters.Select(tp => tp.Name).ToArray();

        var variants = new List<BenchVariant>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            if (!method.GetAttributes().Any(a => a.AttributeClass?.Name == "VariantAttribute"))
                continue;

            var fields = method.Parameters.Select(p =>
            {
                var typeInfo = p.Type;
                return new BenchField(
                    p.Name,
                    typeInfo.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    typeInfo.IsUnmanagedType,
                    typeInfo.IsReferenceType,
                    GetKnownSize(typeInfo));
            }).ToArray();

            variants.Add(new BenchVariant(method.Name, fields));
        }

        if (variants.Count == 0) return null;

        // Detect field name conflicts (same name, different type across variants)
        var fieldTypes = new Dictionary<string, string>();
        bool hasConflicts = false;
        foreach (var v in variants)
            foreach (var f in v.Fields)
            {
                if (!fieldTypes.ContainsKey(f.Name))
                    fieldTypes[f.Name] = f.TypeFullName;
                else if (fieldTypes[f.Name] != f.TypeFullName)
                    hasConflicts = true;
            }

        return new BenchmarkDef(ns, symbol.Name, prefix, isGeneric, hasConflicts, typeParams, variants.ToArray());
    }

    static int? GetKnownSize(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Boolean) return 1;
        if (type.SpecialType == SpecialType.System_Byte) return 1;
        if (type.SpecialType == SpecialType.System_SByte) return 1;
        if (type.SpecialType == SpecialType.System_Int16) return 2;
        if (type.SpecialType == SpecialType.System_UInt16) return 2;
        if (type.SpecialType == SpecialType.System_Char) return 2;
        if (type.SpecialType == SpecialType.System_Int32) return 4;
        if (type.SpecialType == SpecialType.System_UInt32) return 4;
        if (type.SpecialType == SpecialType.System_Single) return 4;
        if (type.SpecialType == SpecialType.System_Int64) return 8;
        if (type.SpecialType == SpecialType.System_UInt64) return 8;
        if (type.SpecialType == SpecialType.System_Double) return 8;
        return null;
    }

    #endregion

    #region Type Declarations

    static string EmitStructDeclaration(BenchmarkDef b, string typeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using Houtamelo.Spire;");
        sb.AppendLine();
        if (b.Namespace.Length > 0) { sb.AppendLine($"namespace {b.Namespace}"); sb.AppendLine("{"); }

        var tp = b.IsGeneric ? "<" + string.Join(", ", b.TypeParams) + ">" : "";
        sb.AppendLine($"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"public partial struct {typeName}{tp}");
        sb.AppendLine("{");
        foreach (var v in b.Variants)
        {
            var paramList = string.Join(", ", v.Fields.Select(f => $"{f.TypeFullName} {f.Name}"));
            sb.AppendLine($"    [Variant] public static partial {typeName}{tp} {v.Name}({paramList});");
        }
        sb.AppendLine("}");

        if (b.Namespace.Length > 0) sb.AppendLine("}");
        return sb.ToString();
    }

    static string EmitRecordDeclaration(BenchmarkDef b, string typeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Houtamelo.Spire;");
        sb.AppendLine();
        if (b.Namespace.Length > 0) { sb.AppendLine($"namespace {b.Namespace}"); sb.AppendLine("{"); }

        var tp = b.IsGeneric ? "<" + string.Join(", ", b.TypeParams) + ">" : "";
        sb.AppendLine($"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"public abstract partial record {typeName}{tp}");
        sb.AppendLine("{");
        foreach (var v in b.Variants)
        {
            var paramList = string.Join(", ", v.Fields.Select(f => $"{f.TypeFullName} {f.Name}"));
            sb.AppendLine($"    public sealed partial record {v.Name}({paramList}) : {typeName}{tp};");
        }
        sb.AppendLine("}");

        if (b.Namespace.Length > 0) sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Model Construction

    static UnionDeclaration MakeUnionDeclaration(
        BenchmarkDef b, string typeName, string keyword, EmitStrategy strategy,
        JsonLibrary json = JsonLibrary.None, bool hasInit = false)
    {
        var variants = b.Variants.Select(v => new VariantInfo(
            v.Name,
            new EquatableArray<FieldInfo>(v.Fields.Select(f =>
                new FieldInfo(f.Name, f.TypeFullName, f.IsUnmanaged, f.IsReferenceType, f.KnownSize, null)
            ).ToImmutableArray()),
            null, "public"
        )).ToArray();

        return new UnionDeclaration(
            Namespace: b.Namespace,
            TypeName: typeName,
            AccessibilityKeyword: "public",
            DeclarationKeyword: keyword,
            IsReadonly: false,
            IsRefStruct: false,
            Strategy: strategy,
            GenerateDeconstruct: true,
            TypeParameters: new EquatableArray<string>(b.TypeParams.ToImmutableArray()),
            Variants: new EquatableArray<VariantInfo>(variants.ToImmutableArray()),
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty),
            Diagnostic: null,
            Json: json,
            JsonDiscriminator: "kind",
            HasInitProperties: hasInit);
    }

    static string StrategyName(EmitStrategy s) => s switch
    {
        EmitStrategy.Additive => "Additive",
        EmitStrategy.BoxedFields => "BoxedFields",
        EmitStrategy.BoxedTuple => "BoxedTuple",
        EmitStrategy.Overlap => "Overlap",
        EmitStrategy.UnsafeOverlap => "UnsafeOverlap",
        _ => s.ToString(),
    };

    static string Emit(UnionDeclaration union) => union.Strategy switch
    {
        EmitStrategy.Additive => AdditiveEmitter.Emit(union),
        EmitStrategy.Overlap => OverlapEmitter.Emit(union),
        EmitStrategy.BoxedFields => BoxedFieldsEmitter.Emit(union),
        EmitStrategy.BoxedTuple => BoxedTupleEmitter.Emit(union),
        _ => "// Unknown strategy",
    };

    #endregion

    #region Benchmark Code

    static string EmitBenchmarkCode(BenchmarkDef b, EmitStrategy[] layouts, CompilationInfo compInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using BenchmarkDotNet.Attributes;");
        sb.AppendLine("using BenchmarkDotNet.Columns;");
        sb.AppendLine("using BenchmarkDotNet.Configs;");
        sb.AppendLine("using BenchmarkDotNet.Diagnosers;");
        sb.AppendLine("using System;");
        if (compInfo.HasSystemTextJson)
            sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();

        var hasNs = b.Namespace.Length > 0;
        if (hasNs) { sb.AppendLine($"namespace {b.Namespace}"); sb.AppendLine("{"); }

        // Collect all type names
        var structTypes = layouts
            .Where(l => l != EmitStrategy.UnsafeOverlap || compInfo.AllowsUnsafe)
            .Select(l => b.Prefix + StrategyName(l)).ToArray();
        var refTypes = new[] { $"{b.Prefix}Record" };
        var allTypes = structTypes.Concat(refTypes).ToArray();

        EmitFiller(sb, b, allTypes, refTypes);
        sb.AppendLine();

        // Property/Deconstruct benchmarks require public properties (no name conflicts)
        if (!b.HasFieldNameConflicts)
        {
            EmitPropertyBenchmark(sb, b, allTypes, structTypes, refTypes);
            sb.AppendLine();
        }

        EmitConstructBenchmark(sb, b, allTypes);
        sb.AppendLine();
        EmitCopyBenchmark(sb, b, allTypes);

        if (!b.HasFieldNameConflicts && compInfo.HasSystemTextJson)
        {
            sb.AppendLine();
            EmitJsonStjBenchmark(sb, b, allTypes, refTypes);
        }
        if (!b.HasFieldNameConflicts && compInfo.HasNewtonsoftJson)
        {
            sb.AppendLine();
            EmitJsonNsjBenchmark(sb, b, allTypes, refTypes);
        }

        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    static void EmitFiller(StringBuilder sb, BenchmarkDef b, string[] allTypes, string[] refTypes)
    {
        sb.AppendLine($"static class {b.Prefix}Filler");
        sb.AppendLine("{");

        foreach (var typeName in allTypes)
        {
            var isRef = refTypes.Contains(typeName);
            sb.AppendLine($"    public static void Fill({typeName}[] arr, Random rng, int dist)");
            sb.AppendLine("    {");
            sb.AppendLine("        for (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            int v = dist == 1 && rng.NextDouble() < 0.8 ? 0 : i % {b.Variants.Length};");
            sb.AppendLine("            arr[i] = v switch");
            sb.AppendLine("            {");

            for (int vi = 0; vi < b.Variants.Length; vi++)
            {
                var v = b.Variants[vi];
                var args = string.Join(", ", v.Fields.Select(f => RandomExpr(f)));
                var ctor = isRef
                    ? $"new {typeName}.{v.Name}({args})"
                    : $"{typeName}.{v.Name}({args})";
                var lhs = vi == b.Variants.Length - 1 ? "_" : vi.ToString();
                sb.AppendLine($"                {lhs} => {ctor},");
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
    }

    static void EmitPropertyBenchmark(StringBuilder sb, BenchmarkDef b,
        string[] allTypes, string[] structTypes, string[] refTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine("[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]");
        sb.AppendLine("[CategoriesColumn]");
        sb.AppendLine($"public class {b.Prefix}PropertyBenchmarks");
        sb.AppendLine("{");
        sb.AppendLine("    [Params(Houtamelo.Spire.Benchmarks.Helpers.BenchN.Default)] public int N { get; set; }");
        sb.AppendLine();

        foreach (var t in allTypes)
            sb.AppendLine($"    {t}[] _{Short(t, b.Prefix)} = null!;");
        sb.AppendLine();

        sb.AppendLine("    [GlobalSetup] public void Setup()");
        sb.AppendLine("    {");
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"        _{sn} = new {t}[N]; {b.Prefix}Filler.Fill(_{sn}, new Random(42), 0);");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        bool first = true;
        foreach (var t in structTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"Property\"), Benchmark{bl}Description = \"{sn}\")]");
            sb.AppendLine($"    public double P_{sn}()");
            sb.AppendLine("    {");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine($"        var arr = _{sn};");
            sb.AppendLine("        for (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (arr[i])");
            sb.AppendLine("            {");
            EmitStructPropertyArms(sb, b, t);
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return sum;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var t in refTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"    [BenchmarkCategory(\"Property\"), Benchmark(Description = \"{sn}\")]");
            sb.AppendLine($"    public double P_{sn}()");
            sb.AppendLine("    {");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine($"        var arr = _{sn};");
            sb.AppendLine("        for (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (arr[i])");
            sb.AppendLine("            {");
            EmitRefPropertyArms(sb, b, t);
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return sum;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ── Deconstruct category: struct types use Deconstruct overloads ──
        // BoxedTuple excluded — only has (Kind, object?) overload, not comparable.

        first = true;
        foreach (var t in structTypes.Where(t => !t.EndsWith("BoxedTuple")))
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"Deconstruct\"), Benchmark{bl}Description = \"{sn}\")]");
            sb.AppendLine($"    public double D_{sn}()");
            sb.AppendLine("    {");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine($"        var arr = _{sn};");
            sb.AppendLine("        for (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var e = arr[i];");
            sb.AppendLine("            switch (e.kind)");
            sb.AppendLine("            {");
            EmitStructDeconstructArms(sb, b, t);
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return sum;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Record: positional deconstruct
        foreach (var t in refTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"    [BenchmarkCategory(\"Deconstruct\"), Benchmark(Description = \"{sn}\")]");
            sb.AppendLine($"    public double D_{sn}()");
            sb.AppendLine("    {");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine($"        var arr = _{sn};");
            sb.AppendLine("        for (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (arr[i])");
            sb.AppendLine("            {");
            EmitRecordDeconstructArms(sb, b, t);
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return sum;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    static void EmitConstructBenchmark(StringBuilder sb, BenchmarkDef b, string[] allTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine($"public class {b.Prefix}ConstructBenchmarks");
        sb.AppendLine("{");
        sb.AppendLine("    [Params(Houtamelo.Spire.Benchmarks.Helpers.BenchN.Default)] public int N { get; set; }");
        sb.AppendLine();

        bool first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [Benchmark{bl}Description = \"{sn}\")]");
            sb.AppendLine($"    public {t}[] C_{sn}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        var arr = new {t}[N];");
            sb.AppendLine($"        {b.Prefix}Filler.Fill(arr, new Random(42), 0);");
            sb.AppendLine("        return arr;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    static void EmitCopyBenchmark(StringBuilder sb, BenchmarkDef b, string[] allTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine($"public class {b.Prefix}CopyBenchmarks");
        sb.AppendLine("{");
        sb.AppendLine("    [Params(Houtamelo.Spire.Benchmarks.Helpers.BenchN.Default)] public int N { get; set; }");
        sb.AppendLine();

        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"    {t}[] _s{sn} = null!; {t}[] _d{sn} = null!;");
        }
        sb.AppendLine();

        sb.AppendLine("    [GlobalSetup] public void Setup()");
        sb.AppendLine("    {");
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"        _s{sn} = new {t}[N]; _d{sn} = new {t}[N]; {b.Prefix}Filler.Fill(_s{sn}, new Random(42), 0);");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        bool first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [Benchmark{bl}Description = \"{sn}\")] public void Cp_{sn}() => Array.Copy(_s{sn}, _d{sn}, N);");
        }

        sb.AppendLine("}");
    }

    static void EmitJsonStjBenchmark(StringBuilder sb, BenchmarkDef b, string[] allTypes, string[] refTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine("[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]");
        sb.AppendLine("[CategoriesColumn]");
        sb.AppendLine($"public class {b.Prefix}JsonStjBenchmarks");
        sb.AppendLine("{");
        sb.AppendLine("    [Params(Houtamelo.Spire.Benchmarks.Helpers.BenchN.Default)] public int N { get; set; }");
        sb.AppendLine("    JsonSerializerOptions _opts = null!;");
        sb.AppendLine();

        foreach (var t in allTypes)
            sb.AppendLine($"    {t}[] _{Short(t, b.Prefix)}Arr = null!; string _{Short(t, b.Prefix)}Json = null!;");
        sb.AppendLine();

        sb.AppendLine("    [GlobalSetup] public void Setup()");
        sb.AppendLine("    {");
        sb.AppendLine("        _opts = new JsonSerializerOptions();");
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"        _{sn}Arr = new {t}[N]; {b.Prefix}Filler.Fill(_{sn}Arr, new Random(42), 0);");
            sb.AppendLine($"        _{sn}Json = JsonSerializer.Serialize(_{sn}Arr, _opts);");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        bool first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"STJ Serialize\"), Benchmark{bl}Description = \"{sn}\")] public string Ser_{sn}() => JsonSerializer.Serialize(_{sn}Arr, _opts);");
        }
        sb.AppendLine();

        first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"STJ Deserialize\"), Benchmark{bl}Description = \"{sn}\")] public {t}[]? Des_{sn}() => JsonSerializer.Deserialize<{t}[]>(_{sn}Json, _opts);");
        }

        sb.AppendLine("}");
    }

    static void EmitJsonNsjBenchmark(StringBuilder sb, BenchmarkDef b, string[] allTypes, string[] refTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine("[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]");
        sb.AppendLine("[CategoriesColumn]");
        sb.AppendLine($"public class {b.Prefix}JsonNsjBenchmarks");
        sb.AppendLine("{");
        sb.AppendLine("    [Params(Houtamelo.Spire.Benchmarks.Helpers.BenchN.Default)] public int N { get; set; }");
        sb.AppendLine();

        foreach (var t in allTypes)
            sb.AppendLine($"    {t}[] _{Short(t, b.Prefix)}Arr = null!; string _{Short(t, b.Prefix)}Json = null!;");
        sb.AppendLine();

        sb.AppendLine("    [GlobalSetup] public void Setup()");
        sb.AppendLine("    {");
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            sb.AppendLine($"        _{sn}Arr = new {t}[N]; {b.Prefix}Filler.Fill(_{sn}Arr, new Random(42), 0);");
            sb.AppendLine($"        _{sn}Json = Newtonsoft.Json.JsonConvert.SerializeObject(_{sn}Arr);");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        bool first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"NSJ Serialize\"), Benchmark{bl}Description = \"{sn}\")] public string NSer_{sn}() => Newtonsoft.Json.JsonConvert.SerializeObject(_{sn}Arr);");
        }
        sb.AppendLine();

        first = true;
        foreach (var t in allTypes)
        {
            var sn = Short(t, b.Prefix);
            var bl = first ? "(Baseline = true, " : "(";
            first = false;
            sb.AppendLine($"    [BenchmarkCategory(\"NSJ Deserialize\"), Benchmark{bl}Description = \"{sn}\")] public {t}[]? NDes_{sn}() => Newtonsoft.Json.JsonConvert.DeserializeObject<{t}[]>(_{sn}Json);");
        }

        sb.AppendLine("}");
    }

    static void EmitStructDeconstructArms(StringBuilder sb, BenchmarkDef b, string typeName)
    {
        // Group variants by field count — replicate DU generator's fieldless-merge logic
        var allGroups = b.Variants
            .GroupBy(v => v.Fields.Length)
            .ToDictionary(g => g.Key, g => g.ToList());

        var hasFieldless = allGroups.ContainsKey(0);
        var groups = allGroups.Where(g => g.Key > 0)
            .ToDictionary(g => g.Key, g => g.Value);

        // DU generator merges fieldless into smallest group when smallest has count 1
        if (hasFieldless && groups.Count > 0)
        {
            var smallestKey = groups.Keys.Min();
            if (groups[smallestKey].Count == 1)
                groups[smallestKey].AddRange(allGroups[0]);
        }

        foreach (var v in b.Variants)
        {
            var numFields = v.Fields.Where(f => IsNumeric(f.TypeFullName)).ToArray();
            if (numFields.Length == 0 && v.Fields.Length == 0) continue;

            sb.AppendLine($"                case {typeName}.Kind.{v.Name}:");
            sb.AppendLine("                {");

            if (v.Fields.Length == 0)
            {
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
                continue;
            }

            var isUniqueArity = groups[v.Fields.Length].Count == 1;
            var vn = v.Name.ToLower(); // unique prefix per variant

            if (isUniqueArity)
            {
                // Typed Deconstruct — use explicit types to avoid ambiguity with generic overload
                var outParams = string.Join(", ", v.Fields.Select(f =>
                    numFields.Any(nf => nf.Name == f.Name)
                        ? $"out {f.TypeFullName} {vn}_{f.Name}"
                        : $"out {f.TypeFullName} _"));
                sb.AppendLine($"                    e.Deconstruct(out _, {outParams});");

                if (numFields.Length > 0)
                {
                    var expr = numFields.Length == 1
                        ? $"{vn}_{numFields[0].Name}"
                        : string.Join(" + ", numFields.Select(f => $"(double){vn}_{f.Name}"));
                    sb.AppendLine($"                    sum += {expr};");
                }
            }
            else
            {
                // Generic Deconstruct — (Kind, object?, object?, ...)
                var outParams = string.Join(", ",
                    Enumerable.Range(0, v.Fields.Length).Select(i => $"out object? {vn}_{i}"));
                sb.AppendLine($"                    e.Deconstruct(out _, {outParams});");

                // Cast numeric fields from object?
                var castExprs = new List<string>();
                for (int i = 0; i < v.Fields.Length; i++)
                {
                    var f = v.Fields[i];
                    if (IsNumeric(f.TypeFullName))
                        castExprs.Add($"({f.TypeFullName}){vn}_{i}!");
                }

                if (castExprs.Count > 0)
                {
                    var expr = castExprs.Count == 1
                        ? castExprs[0]
                        : string.Join(" + ", castExprs.Select(e => $"(double){e}"));
                    sb.AppendLine($"                    sum += {expr};");
                }
            }

            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }
    }

    static void EmitRecordDeconstructArms(StringBuilder sb, BenchmarkDef b, string typeName)
    {
        foreach (var v in b.Variants)
        {
            var numFields = v.Fields.Where(f => IsNumeric(f.TypeFullName)).ToArray();
            if (v.Fields.Length == 0) continue;

            // Record positional deconstruct: case Record.Variant(var a, var b, ...)
            var deconParams = string.Join(", ", v.Fields.Select(f =>
                numFields.Any(nf => nf.Name == f.Name) ? $"var _{f.Name}" : "_"));

            sb.AppendLine($"                case {typeName}.{v.Name}({deconParams}):");

            if (numFields.Length > 0)
            {
                var expr = numFields.Length == 1
                    ? $"_{numFields[0].Name}"
                    : string.Join(" + ", numFields.Select(f => $"(double)_{f.Name}"));
                sb.AppendLine($"                    sum += {expr}; break;");
            }
            else
            {
                sb.AppendLine("                    break;");
            }
        }
    }

    static void EmitStructPropertyArms(StringBuilder sb, BenchmarkDef b, string typeName)
    {
        foreach (var v in b.Variants)
        {
            var numFields = v.Fields.Where(f => IsNumeric(f.TypeFullName)).ToArray();
            if (numFields.Length == 0) continue;

            var props = string.Join(", ", numFields.Select(f => $"{f.Name}: var _{f.Name}"));
            var expr = numFields.Length == 1
                ? $"_{numFields[0].Name}"
                : string.Join(" + ", numFields.Select(f => $"(double)_{f.Name}"));

            sb.AppendLine($"                case {{ kind: {typeName}.Kind.{v.Name}, {props} }}:");
            sb.AppendLine($"                    sum += {expr}; break;");
        }
    }

    static void EmitRefPropertyArms(StringBuilder sb, BenchmarkDef b, string typeName)
    {
        foreach (var v in b.Variants)
        {
            var numFields = v.Fields.Where(f => IsNumeric(f.TypeFullName)).ToArray();
            if (numFields.Length == 0) continue;

            var props = string.Join(", ", numFields.Select(f => $"{f.Name}: var _{f.Name}"));
            var expr = numFields.Length == 1
                ? $"_{numFields[0].Name}"
                : string.Join(" + ", numFields.Select(f => $"(double)_{f.Name}"));

            sb.AppendLine($"                case {typeName}.{v.Name} {{ {props} }}:");
            sb.AppendLine($"                    sum += {expr}; break;");
        }
    }

    #endregion

    #region Utilities

    static string Capitalize(string name)
        => name.Length == 0 ? name : char.ToUpper(name[0]) + name.Substring(1);

    static string Short(string typeName, string prefix)
        => typeName.StartsWith(prefix) ? typeName.Substring(prefix.Length) : typeName;

    static bool IsNumeric(string type)
    {
        var t = type.Replace("global::", "").Replace("System.", "");
        return t is "int" or "Int32" or "float" or "Single"
            or "double" or "Double" or "long" or "Int64"
            or "short" or "Int16" or "byte" or "Byte"
            or "uint" or "UInt32" or "ulong" or "UInt64"
            or "ushort" or "UInt16" or "decimal" or "Decimal"
            or "bool" or "Boolean";
    }

    static string RandomExpr(BenchField f)
    {
        var t = f.TypeFullName.Replace("global::", "").Replace("System.", "");
        return t switch
        {
            "int" or "Int32" => "rng.Next(-1000, 1000)",
            "float" or "Single" => "rng.NextSingle()",
            "double" or "Double" => "rng.NextDouble()",
            "bool" or "Boolean" => "rng.Next(2) == 1",
            "string" or "String" => "\"bench\"",
            "long" or "Int64" => "(long)rng.Next()",
            "byte" or "Byte" => "(byte)rng.Next(256)",
            "short" or "Int16" => "(short)rng.Next(-1000, 1000)",
            _ => $"default({f.TypeFullName})"
        };
    }

    #endregion
}
