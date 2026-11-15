using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Reports authored record-struct variant members whose behavior or Unity
/// serialization metadata needs the author's attention. The analysis runs on
/// the merged compilation, but only source declarations written by the user are
/// considered; generated partial declarations are intentionally ignored.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordVariantDeclarationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE028_RecordVariantSerialization,
            AnalyzerDescriptors.SPIRE029_RecordVariantReconstruction,
            AnalyzerDescriptors.SPIRE030_ReadonlyRecordVariantSerialization);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var compilation = compilationContext.Compilation;
            var discriminatedUnionAttribute = compilation.GetTypeByMetadataName(
                "Houtamelo.Spire.DiscriminatedUnionAttribute");
            var variantAttribute = compilation.GetTypeByMetadataName(
                "Houtamelo.Spire.VariantAttribute");
            if (discriminatedUnionAttribute is null || variantAttribute is null)
                return;

            var serializableAttribute = compilation.GetTypeByMetadataName(
                "System.SerializableAttribute");
            var serializeFieldAttribute = compilation.GetTypeByMetadataName(
                "UnityEngine.SerializeField");
            var serializeReferenceAttribute = compilation.GetTypeByMetadataName(
                "UnityEngine.SerializeReference");

            var isUnitySerialization = IsUnitySerializationCompilation(compilation);
            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeRecordVariant(
                    symbolContext,
                    discriminatedUnionAttribute,
                    variantAttribute,
                    serializableAttribute,
                    serializeFieldAttribute,
                    serializeReferenceAttribute,
                    isUnitySerialization),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeRecordVariant(
        SymbolAnalysisContext context,
        INamedTypeSymbol discriminatedUnionAttribute,
        INamedTypeSymbol variantAttribute,
        INamedTypeSymbol? serializableAttribute,
        INamedTypeSymbol? serializeFieldAttribute,
        INamedTypeSymbol? serializeReferenceAttribute,
        bool isUnitySerialization)
    {
        if (context.Symbol is not INamedTypeSymbol recordVariant ||
            recordVariant.TypeKind != TypeKind.Struct ||
            !recordVariant.IsRecord ||
            recordVariant.ContainingType is not INamedTypeSymbol union ||
            union.TypeKind != TypeKind.Struct ||
            !HasAttribute(recordVariant, variantAttribute) ||
            !HasAttribute(union, discriminatedUnionAttribute) ||
            recordVariant.DeclaredAccessibility != Accessibility.Public ||
            recordVariant.Arity != 0)
        {
            return;
        }

        var declaration = FindUserRecordDeclaration(recordVariant);
        if (declaration is null)
            return;

        var positionalParameters = GetPositionalParameters(declaration);
        ReportReconstructionWarnings(context, recordVariant, declaration, positionalParameters);

        if (isUnitySerialization &&
            HasAttribute(union, serializableAttribute) &&
            !union.IsReadOnly &&
            !union.IsRefLikeType &&
            !union.IsRecord)
        {
            if (recordVariant.IsReadOnly && positionalParameters.Count > 0)
            {
                ReportReadonlySerializationWarning(
                    context,
                    recordVariant,
                    union);
            }
            else
            {
                ReportSerializationWarningIfNeeded(
                    context,
                    recordVariant,
                    union,
                    declaration,
                    positionalParameters,
                    serializableAttribute,
                    serializeFieldAttribute,
                    serializeReferenceAttribute);
            }
        }
    }

    private static void ReportReadonlySerializationWarning(
        SymbolAnalysisContext context,
        INamedTypeSymbol recordVariant,
        INamedTypeSymbol union)
    {
        var readonlyDeclaration = recordVariant.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .Where(declaration => IsUserLocation(declaration.SyntaxTree))
            .FirstOrDefault(declaration => declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
        if (readonlyDeclaration is null)
            return;

        var readonlyToken = readonlyDeclaration.Modifiers
            .First(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        context.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE030_ReadonlyRecordVariantSerialization,
            readonlyToken.GetLocation(),
            recordVariant.Name,
            union.Name));
    }

    private static void ReportReconstructionWarnings(
        SymbolAnalysisContext context,
        INamedTypeSymbol recordVariant,
        RecordDeclarationSyntax declaration,
        IReadOnlyList<ParameterSyntax> positionalParameters)
    {
        if (positionalParameters.Count == 0)
            return;

        foreach (var parameter in positionalParameters)
        {
            var property = FindProperty(recordVariant, parameter.Identifier.ValueText);
            var field = FindField(recordVariant, parameter.Identifier.ValueText);
            var hasAuthoredProperty = HasAuthoredPropertyDeclaration(
                recordVariant,
                parameter.Identifier.ValueText);
            var hasAuthoredField = HasAuthoredFieldDeclaration(
                recordVariant,
                parameter.Identifier.ValueText);
            if ((!hasAuthoredProperty || property is null) &&
                (!hasAuthoredField || field is null))
            {
                continue;
            }

            ISymbol member = hasAuthoredProperty ? property! : field!;
            var location = member.Locations.FirstOrDefault(candidate =>
                    candidate.SourceTree is not null && IsUserLocation(candidate.SourceTree))
                ?? parameter.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE029_RecordVariantReconstruction,
                location,
                recordVariant.Name,
                parameter.Identifier.ValueText));
        }
    }

    private static void ReportSerializationWarningIfNeeded(
        SymbolAnalysisContext context,
        INamedTypeSymbol recordVariant,
        INamedTypeSymbol union,
        RecordDeclarationSyntax declaration,
        IReadOnlyList<ParameterSyntax> positionalParameters,
        INamedTypeSymbol? serializableAttribute,
        INamedTypeSymbol? serializeFieldAttribute,
        INamedTypeSymbol? serializeReferenceAttribute)
    {
        var missingSerializable = !HasAttribute(recordVariant, serializableAttribute);
        var missingFields = positionalParameters.Any(parameter =>
        {
            var managedReferenceRequested = HasManagedReferenceMarker(
                FindPositionalParameterSymbol(recordVariant, positionalParameters, parameter),
                context.Compilation.GetTypeByMetadataName(
                    "Houtamelo.Spire.UnitySerializeReferenceAttribute"));
            return !HasSerializableBackingField(
                recordVariant,
                parameter,
                serializeFieldAttribute,
                serializeReferenceAttribute,
                managedReferenceRequested);
        });
        if (!missingSerializable && !missingFields)
            return;

        var reasons = new List<string>();
        if (missingSerializable)
            reasons.Add("add [Serializable] to the record");
        if (missingFields)
            reasons.Add("annotate each positional backing field with [field: SerializeField] or [field: SerializeReference]; a [UnitySerializeReference] parameter requires [field: SerializeReference]");
        if (declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            reasons.Add("preserve the partial declaration so Spire can generate missing attributes");
        else
            reasons.Add("make the record partial so Spire can generate missing attributes");
        reasons.Add("remove [Serializable] from the union if Unity serialization is not intended");

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("MissingSerializable", missingSerializable ? "true" : "false");
        properties.Add("MissingFields", missingFields ? "true" : "false");
        properties.Add("CanMakePartial", declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ? "false" : "true");

        context.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE028_RecordVariantSerialization,
            declaration.Identifier.GetLocation(),
            properties.ToImmutable(),
            recordVariant.Name,
            union.Name,
            string.Join("; ", reasons)));
    }

    private static bool HasSerializableBackingField(
        INamedTypeSymbol recordVariant,
        ParameterSyntax parameter,
        INamedTypeSymbol? serializeFieldAttribute,
        INamedTypeSymbol? serializeReferenceAttribute,
        bool managedReferenceRequested)
    {
        var property = FindProperty(recordVariant, parameter.Identifier.ValueText);
        var explicitField = FindField(recordVariant, parameter.Identifier.ValueText);
        if (explicitField is not null &&
            (HasAttribute(explicitField, serializeReferenceAttribute) ||
             (!managedReferenceRequested && HasAttribute(explicitField, serializeFieldAttribute))))
            return true;

        if (property is null)
            return false;

        var backingField = recordVariant.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field =>
                SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
        if (backingField is null)
            return false;

        return HasAttribute(backingField, serializeReferenceAttribute) ||
            (!managedReferenceRequested && HasAttribute(backingField, serializeFieldAttribute));
    }

    private static bool HasManagedReferenceMarker(
        IParameterSymbol? parameter,
        INamedTypeSymbol? expectedAttribute)
    {
        if (parameter is null || expectedAttribute is null)
            return false;

        return parameter.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, expectedAttribute));
    }

    private static IParameterSymbol? FindPositionalParameterSymbol(
        INamedTypeSymbol recordVariant,
        IReadOnlyList<ParameterSyntax> positionalParameters,
        ParameterSyntax parameter)
    {
        var names = positionalParameters
            .Select(candidate => candidate.Identifier.ValueText)
            .ToArray();
        var constructor = recordVariant.Constructors.FirstOrDefault(candidate =>
            candidate.Parameters.Length == names.Length &&
            candidate.Parameters.Select(candidateParameter => candidateParameter.Name)
                .SequenceEqual(names, StringComparer.Ordinal));
        return constructor?.Parameters.FirstOrDefault(candidate =>
            candidate.Name == parameter.Identifier.ValueText);
    }

    private static IPropertySymbol? FindProperty(
        INamedTypeSymbol recordVariant,
        string name)
        => recordVariant.GetMembers(name)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(property => !property.IsStatic);

    private static IFieldSymbol? FindField(
        INamedTypeSymbol recordVariant,
        string name)
        => recordVariant.GetMembers(name)
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => !field.IsStatic);

    private static bool HasAuthoredPropertyDeclaration(
        INamedTypeSymbol recordVariant,
        string propertyName)
        => recordVariant.DeclaringSyntaxReferences
            .Where(reference => IsUserLocation(reference.SyntaxTree))
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .SelectMany(record => record.Members.OfType<PropertyDeclarationSyntax>())
            .Any(property => property.Identifier.ValueText == propertyName);

    private static bool HasAuthoredFieldDeclaration(
        INamedTypeSymbol recordVariant,
        string fieldName)
        => recordVariant.DeclaringSyntaxReferences
            .Where(reference => IsUserLocation(reference.SyntaxTree))
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .SelectMany(record => record.Members.OfType<FieldDeclarationSyntax>())
            .SelectMany(field => field.Declaration.Variables)
            .Any(variable => variable.Identifier.ValueText == fieldName);

    private static RecordDeclarationSyntax? FindUserRecordDeclaration(
        INamedTypeSymbol recordVariant)
        => recordVariant.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .Where(declaration => IsUserLocation(declaration.SyntaxTree))
            .OrderByDescending(declaration => declaration.ParameterList is not null)
            .ThenBy(declaration => declaration.SpanStart)
            .FirstOrDefault();

    private static IReadOnlyList<ParameterSyntax> GetPositionalParameters(
        RecordDeclarationSyntax declaration)
        => declaration.ParameterList?.Parameters.ToArray()
            ?? Array.Empty<ParameterSyntax>();

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? expected)
        => expected is not null && symbol.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, expected));

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(
                attribute.AttributeClass?.ToDisplayString(),
                metadataName,
                StringComparison.Ordinal));

    private static bool IsUnitySerializationCompilation(Compilation compilation)
        => CompilationInfo.IsUnityCompilation(compilation) &&
            compilation.GetTypeByMetadataName("UnityEngine.ISerializationCallbackReceiver") is not null &&
            compilation.GetTypeByMetadataName("UnityEngine.SerializeField") is not null &&
            compilation.GetTypeByMetadataName("UnityEngine.SerializeReference") is not null;

    private static bool IsUserLocation(SyntaxTree tree)
    {
        var path = tree.FilePath;
        return string.IsNullOrEmpty(path) ||
            !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
    }
}
