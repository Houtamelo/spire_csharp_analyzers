using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Attributes;
using Houtamelo.Spire.Analyzers.SourceGenerators.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

/// Expands a benchmark declaration into the current payload representations.
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
            transform: static (ctx, _) => ParseBenchmarkUnion(ctx))
            .Where(static value => value is not null);

        var compilationInfo = context.CompilationProvider.Select(static (compilation, _) =>
            new CompilationInfo(
                HasSystemTextJson: compilation.GetTypeByMetadataName(
                    "System.Text.Json.Serialization.JsonConverter`1") is not null,
                HasNewtonsoftJson: compilation.GetTypeByMetadataName(
                    "Newtonsoft.Json.JsonConverter") is not null,
                HasInitProperties: CompilationInfo.HasAccessibleType(
                    compilation, "System.Runtime.CompilerServices.IsExternalInit")));

        context.RegisterSourceOutput(benchmarks.Combine(compilationInfo), static (ctx, pair) =>
        {
            var (benchmark, info) = pair;
            if (benchmark is not { } definition)
                return;

            EmitStruct(ctx, definition, definition.InputTypeName, ReferenceStorage.Auto, info);
            EmitStruct(ctx, definition, definition.Prefix + "Auto", ReferenceStorage.Auto, info);
            EmitStruct(ctx, definition, definition.Prefix + "Typed", ReferenceStorage.Typed, info);
            EmitStruct(ctx, definition, definition.Prefix + "Shared", ReferenceStorage.Shared, info);

            var recordName = definition.Prefix + "Record";
            ctx.AddSource($"{recordName}.BenchDecl.g.cs", EmitRecordDeclaration(definition, recordName));
            var record = MakeUnionDeclaration(definition, recordName, "record", ReferenceStorage.Auto, info);
            ctx.AddSource($"{recordName}.Bench.g.cs", RecordEmitter.Emit(record));

            EmitJson(ctx, definition, record);
            foreach (var storage in new[] { ReferenceStorage.Auto, ReferenceStorage.Typed, ReferenceStorage.Shared })
            {
                var union = MakeUnionDeclaration(definition, definition.Prefix + StorageName(storage), "struct", storage, info);
                EmitJson(ctx, definition, union);
            }

            if (!definition.IsGeneric)
                ctx.AddSource($"{definition.Prefix}.BenchCode.g.cs", EmitBenchmarkCode(definition));
        });
    }

    private static void EmitStruct(
        SourceProductionContext context,
        BenchmarkDef definition,
        string typeName,
        ReferenceStorage referenceStorage,
        CompilationInfo info)
    {
        if (typeName != definition.InputTypeName)
            context.AddSource($"{typeName}.BenchDecl.g.cs", EmitStructDeclaration(definition, typeName));

        var union = MakeUnionDeclaration(definition, typeName, "struct", referenceStorage, info);
        context.AddSource($"{typeName}.Bench.g.cs", VariantPayloadEmitter.Emit(union));
    }

    private static void EmitJson(SourceProductionContext context, BenchmarkDef definition, UnionDeclaration union)
    {
        if (definition.HasFieldNameConflicts)
            return;
        if ((union.Json & JsonLibrary.SystemTextJson) != 0)
            context.AddSource($"{union.TypeName}.Stj.g.cs", SystemTextJsonEmitter.Emit(union));
        if ((union.Json & JsonLibrary.NewtonsoftJson) != 0)
            context.AddSource($"{union.TypeName}.Nsj.g.cs", NewtonsoftJsonEmitter.Emit(union));
    }

    private sealed record BenchmarkDef(
        string Namespace,
        string InputTypeName,
        string Prefix,
        bool IsGeneric,
        bool HasFieldNameConflicts,
        ImmutableArray<string> TypeParameters,
        ImmutableArray<string> TypeParameterConstraints,
        ImmutableArray<VariantInfo> Variants);

    private static BenchmarkDef? ParseBenchmarkUnion(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        var prefix = symbol.GetAttributes()
            .Where(attribute => attribute.AttributeClass?.Name == "BenchmarkUnionAttribute")
            .SelectMany(attribute => attribute.NamedArguments)
            .Where(argument => argument.Key == "Name")
            .Select(argument => argument.Value.Value as string)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value))
            ?? symbol.Name;

        var variants = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic && method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "VariantAttribute"))
            .Select(method => new VariantInfo(
                method.Name,
                new EquatableArray<FieldInfo>(method.Parameters.Select(parameter =>
                    new FieldInfo(
                        Name: parameter.Name,
                        TypeFullName: parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsUnmanaged: UnionParser.IsUnmanagedAfterGeneration(parameter.Type),
                        IsReferenceType: parameter.Type.IsReferenceType,
                        JsonName: null,
                        ConstructorTypeKey: parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsGenericDependent: ContainsTypeParameter(parameter.Type))).ToImmutableArray()),
                JsonName: null,
                AccessibilityKeyword: AccessibilityToKeyword(method.DeclaredAccessibility)))
            .ToImmutableArray();

        if (variants.Length == 0)
            return null;

        var conflictNames = variants.SelectMany(variant => variant.Fields)
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .Any(group => group.Select(field => field.TypeFullName).Distinct(StringComparer.Ordinal).Skip(1).Any());

        return new BenchmarkDef(
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString(),
            InputTypeName: symbol.Name,
            Prefix: prefix,
            IsGeneric: symbol.TypeParameters.Length > 0,
            HasFieldNameConflicts: conflictNames,
            TypeParameters: symbol.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray(),
            TypeParameterConstraints: GetTypeParameterConstraints(symbol),
            Variants: variants);
    }

    private static UnionDeclaration MakeUnionDeclaration(
        BenchmarkDef definition,
        string typeName,
        string declarationKeyword,
        ReferenceStorage referenceStorage,
        CompilationInfo info)
    {
        var json = JsonLibrary.None;
        if (info.HasSystemTextJson)
            json |= JsonLibrary.SystemTextJson;
        if (info.HasNewtonsoftJson)
            json |= JsonLibrary.NewtonsoftJson;

        return new UnionDeclaration(
            Namespace: definition.Namespace,
            TypeName: typeName,
            AccessibilityKeyword: "public",
            DeclarationKeyword: declarationKeyword,
            IsReadonly: false,
            IsRefStruct: false,
            Strategy: declarationKeyword == "record" ? EmitStrategy.Record : EmitStrategy.VariantPayload,
            TypeParameters: new EquatableArray<string>(definition.TypeParameters),
            Variants: new EquatableArray<VariantInfo>(definition.Variants),
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty),
            Diagnostic: null,
            Json: json,
            JsonDiscriminator: "kind",
            HasInitProperties: info.HasInitProperties,
            RawConfig: new RawAttributeConfig(referenceStorage, (int)json, "kind"),
            IsGeneric: definition.IsGeneric)
        {
            ReferenceStorage = referenceStorage,
            TypeParameterConstraints = new EquatableArray<string>(definition.TypeParameterConstraints),
        };
    }

    private static string EmitStructDeclaration(BenchmarkDef definition, string typeName)
    {
        var sb = new SourceBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using Houtamelo.Spire;");
        OpenNamespace(sb, definition.Namespace);
        var parameters = FormatTypeParameters(definition.TypeParameters);
        sb.AppendLine($"public partial struct {typeName}{parameters}");
        sb.OpenBrace();
        foreach (var variant in definition.Variants)
        {
            var fields = string.Join(", ", variant.Fields.Select(field =>
                $"{field.TypeFullName} {IdentifierUtility.Escape(field.Name)}"));
            sb.AppendLine($"[Variant] public static partial {typeName}{parameters} {IdentifierUtility.Escape(variant.Name)}({fields});");
        }
        sb.CloseBrace();
        CloseNamespace(sb, definition.Namespace);
        return sb.ToString();
    }

    private static string EmitRecordDeclaration(BenchmarkDef definition, string typeName)
    {
        var sb = new SourceBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        OpenNamespace(sb, definition.Namespace);
        var parameters = FormatTypeParameters(definition.TypeParameters);
        sb.AppendLine($"public abstract partial record {typeName}{parameters}");
        sb.OpenBrace();
        foreach (var variant in definition.Variants)
        {
            var fields = string.Join(", ", variant.Fields.Select(field =>
                $"{field.TypeFullName} {IdentifierUtility.Escape(field.Name)}"));
            sb.AppendLine($"public sealed partial record {variant.Name}({fields}) : {typeName}{parameters};");
        }
        sb.CloseBrace();
        CloseNamespace(sb, definition.Namespace);
        return sb.ToString();
    }

    private static string EmitBenchmarkCode(BenchmarkDef definition)
    {
        var structTypes = new[] { definition.InputTypeName, definition.Prefix + "Auto", definition.Prefix + "Typed", definition.Prefix + "Shared" };
        var allTypes = structTypes.Concat(new[] { definition.Prefix + "Record" }).ToArray();
        var sb = new SourceBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using BenchmarkDotNet.Attributes;");
        sb.AppendLine("using Houtamelo.Spire.Benchmarks.Helpers;");
        OpenNamespace(sb, definition.Namespace);
        EmitFiller(sb, definition, allTypes);
        EmitPropertyBenchmarks(sb, definition, structTypes);
        EmitConstructionBenchmarks(sb, definition, allTypes);
        EmitCopyBenchmarks(sb, definition, allTypes);
        CloseNamespace(sb, definition.Namespace);
        return sb.ToString();
    }

    private static void EmitFiller(SourceBuilder sb, BenchmarkDef definition, IReadOnlyList<string> types)
    {
        sb.AppendLine($"static class {definition.Prefix}Filler");
        sb.OpenBrace();
        foreach (var type in types)
        {
            var record = type.EndsWith("Record", StringComparison.Ordinal);
            sb.AppendLine($"public static void Fill({type}[] values, System.Random random)");
            sb.OpenBrace();
            sb.AppendLine("for (var index = 0; index < values.Length; index++)");
            sb.OpenBrace();
            sb.AppendLine($"values[index] = (index % {definition.Variants.Length}) switch");
            sb.OpenBrace();
            for (var index = 0; index < definition.Variants.Length; index++)
            {
                var variant = definition.Variants[index];
                var arguments = string.Join(", ", variant.Fields.Select(RandomExpression));
                var value = record
                    ? $"new {type}.{variant.Name}({arguments})"
                    : $"{type}.{IdentifierUtility.Escape(variant.Name)}({arguments})";
                sb.AppendLine($"{index} => {value},");
            }
            var fallback = record
                ? $"new {type}.{definition.Variants[0].Name}()"
                : $"{type}.{IdentifierUtility.Escape(definition.Variants[0].Name)}()";
            sb.AppendLine($"_ => {fallback},");
            sb.CloseBrace(";");
            sb.CloseBrace();
            sb.CloseBrace();
        }
        sb.CloseBrace();
    }

    private static void EmitPropertyBenchmarks(SourceBuilder sb, BenchmarkDef definition, IReadOnlyList<string> structTypes)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine($"public class {definition.Prefix}PropertyBenchmarks");
        sb.OpenBrace();
        sb.AppendLine("[Params(BenchN.Default)] public int N { get; set; }");
        foreach (var type in structTypes)
            sb.AppendLine($"private {type}[] _{type} = null!;");
        sb.AppendLine("[GlobalSetup] public void Setup()");
        sb.OpenBrace();
        foreach (var type in structTypes)
            sb.AppendLine($"_{type} = new {type}[N]; {definition.Prefix}Filler.Fill(_{type}, new System.Random(42));");
        sb.CloseBrace();
        foreach (var type in structTypes)
        {
            sb.AppendLine($"[Benchmark] public double Read{type}()");
            sb.OpenBrace();
            sb.AppendLine("double sum = 0;");
            sb.AppendLine($"foreach (var value in _{type})");
            sb.OpenBrace();
            EmitNumericArms(sb, definition, type);
            sb.CloseBrace();
            sb.AppendLine("return sum;");
            sb.CloseBrace();
        }
        sb.CloseBrace();
    }

    private static void EmitNumericArms(SourceBuilder sb, BenchmarkDef definition, string typeName)
    {
        foreach (var variant in definition.Variants)
        {
            var numeric = variant.Fields.Where(field => IsNumeric(field.TypeFullName)).ToArray();
            if (numeric.Length == 0)
                continue;
            var captures = string.Join(", ", numeric.Select(field =>
                $"{IdentifierUtility.Escape(field.Name)}: var __{IdentifierUtility.Escape(field.Name)}"));
            var body = numeric.Length == 1
                ? $"__{IdentifierUtility.Escape(numeric[0].Name)}"
                : string.Join(" + ", numeric.Select(field => $"(double)__{IdentifierUtility.Escape(field.Name)}"));
            var accessor = VariantNaming.AccessorName(variant.Name, definition.Variants.Select(candidate => candidate.Name));
            sb.AppendLine($"switch (value) {{ case {{ kind: {typeName}.Kind.{IdentifierUtility.Escape(variant.Name)}, {IdentifierUtility.Escape(accessor)}: {{ {captures} }} }}: sum += {body}; break; }}");
        }
    }

    private static void EmitConstructionBenchmarks(SourceBuilder sb, BenchmarkDef definition, IReadOnlyList<string> types)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine($"public class {definition.Prefix}ConstructionBenchmarks");
        sb.OpenBrace();
        sb.AppendLine("[Params(BenchN.Default)] public int N { get; set; }");
        foreach (var type in types)
            sb.AppendLine($"[Benchmark] public {type}[] Construct{type}() {{ var values = new {type}[N]; {definition.Prefix}Filler.Fill(values, new System.Random(42)); return values; }}");
        sb.CloseBrace();
    }

    private static void EmitCopyBenchmarks(SourceBuilder sb, BenchmarkDef definition, IReadOnlyList<string> types)
    {
        sb.AppendLine("[MemoryDiagnoser]");
        sb.AppendLine($"public class {definition.Prefix}CopyBenchmarks");
        sb.OpenBrace();
        sb.AppendLine("[Params(BenchN.Default)] public int N { get; set; }");
        foreach (var type in types)
            sb.AppendLine($"[Benchmark] public {type}[] Copy{type}() {{ var source = new {type}[N]; var destination = new {type}[N]; {definition.Prefix}Filler.Fill(source, new System.Random(42)); System.Array.Copy(source, destination, N); return destination; }}");
        sb.CloseBrace();
    }

    private static string StorageName(ReferenceStorage storage) => storage switch
    {
        ReferenceStorage.Typed => "Typed",
        ReferenceStorage.Shared => "Shared",
        _ => "Auto",
    };

    private static bool ContainsTypeParameter(ITypeSymbol type)
        => type.TypeKind == TypeKind.TypeParameter || type is IArrayTypeSymbol array && ContainsTypeParameter(array.ElementType) || type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter);

    private static ImmutableArray<string> GetTypeParameterConstraints(INamedTypeSymbol symbol)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var parameter in symbol.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint) constraints.Add("struct");
            else if (parameter.HasReferenceTypeConstraint) constraints.Add("class");
            else if (parameter.HasNotNullConstraint) constraints.Add("notnull");
            constraints.AddRange(parameter.ConstraintTypes.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            if (parameter.HasConstructorConstraint) constraints.Add("new()");
            if (constraints.Count > 0)
                result.Add($"where {IdentifierUtility.Escape(parameter.Name)} : {string.Join(", ", constraints)}");
        }
        return result.ToImmutable();
    }

    private static string AccessibilityToKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => string.Empty,
    };

    private static string FormatTypeParameters(ImmutableArray<string> parameters)
        => parameters.Length == 0 ? string.Empty : "<" + string.Join(", ", parameters.Select(IdentifierUtility.Escape)) + ">";

    private static string RandomExpression(FieldInfo field)
    {
        var type = field.TypeFullName.Replace("global::", string.Empty).Replace("System.", string.Empty);
        return type switch
        {
            "float" or "Single" => "random.NextSingle()",
            "double" or "Double" => "random.NextDouble()",
            "int" or "Int32" => "random.Next()",
            "long" or "Int64" => "(long)random.Next()",
            "string" or "String" => "\"bench\"",
            _ => $"default({field.TypeFullName})",
        };
    }

    private static bool IsNumeric(string type)
    {
        var value = type.Replace("global::", string.Empty).Replace("System.", string.Empty);
        return value is "float" or "Single" or "double" or "Double" or "int" or "Int32" or "long" or "Int64" or "short" or "Int16" or "byte" or "Byte" or "uint" or "UInt32" or "ulong" or "UInt64" or "ushort" or "UInt16";
    }

    private static void OpenNamespace(SourceBuilder sb, string ns)
    {
        if (ns.Length == 0)
            return;
        sb.AppendLine($"namespace {ns}");
        sb.OpenBrace();
    }

    private static void CloseNamespace(SourceBuilder sb, string ns)
    {
        if (ns.Length > 0)
            sb.CloseBrace();
    }
}
