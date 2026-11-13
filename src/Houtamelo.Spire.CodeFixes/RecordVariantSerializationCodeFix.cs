using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

/// Adds the Unity serialization declarations required by an authored record
/// variant. The analyzer reports one diagnostic on the record declaration and
/// stores the applicable actions in diagnostic properties.
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class RecordVariantSerializationCodeFix : CodeFixProvider
{
    private enum FieldSerializationKind
    {
        None,
        SerializeField,
        SerializeReference
    }

    private const string AddSerializableKey = "Spire.RecordVariant.AddSerializable";
    private const string AddFieldAttributesKey = "Spire.RecordVariant.AddFieldAttributes";
    private const string MakePartialKey = "Spire.RecordVariant.MakePartial";

    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("SPIRE028");

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null || diagnostic.Id != "SPIRE028")
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (root is null || semanticModel is null || FindRecord(root, diagnostic) is null)
            return;

        if (diagnostic.Properties.GetValueOrDefault("MissingSerializable") == "true")
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add [Serializable] to the record variant",
                    ct => AddSerializableAsync(context.Document, diagnostic, ct),
                    AddSerializableKey),
                diagnostic);
        }

        if (diagnostic.Properties.GetValueOrDefault("MissingFields") == "true" &&
            await CanApplyFieldFixAsync(context.Document, diagnostic, semanticModel, context.CancellationToken))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add Unity serialization attributes to positional fields",
                    ct => AddFieldAttributesSolutionAsync(context.Document, diagnostic, ct),
                    AddFieldAttributesKey),
                diagnostic);
        }

        if (diagnostic.Properties.GetValueOrDefault("CanMakePartial") == "true")
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the record variant partial",
                    ct => MakePartialAsync(context.Document, diagnostic, ct),
                    MakePartialKey),
                diagnostic);
        }
    }

    private static async Task<Document> AddSerializableAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null || FindRecord(root, diagnostic) is not { } record)
            return document;

        var attribute = ParseAttributeList("[global::System.Serializable]");
        var updated = record.WithAttributeLists(record.AttributeLists.Add(attribute));
        return document.WithSyntaxRoot(root.ReplaceNode(record, updated));
    }

    private static async Task<Document> MakePartialAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null || FindRecord(root, diagnostic) is not { } record)
            return document;

        if (record.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            return document;

        var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = record.Modifiers.Add(partial);
        var updated = record.WithModifiers(modifiers);
        return document.WithSyntaxRoot(root.ReplaceNode(record, updated));
    }

    private static async Task<Solution> AddFieldAttributesSolutionAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (model is null || root is null || FindRecord(root, diagnostic) is not { } record)
            return document.Project.Solution;

        var recordSymbol = model.GetDeclaredSymbol(record, cancellationToken) as INamedTypeSymbol;
        if (recordSymbol is null)
            return document.Project.Solution;

        var solution = document.Project.Solution;
        foreach (var projectDocument in document.Project.Documents)
        {
            var projectRoot = await projectDocument.GetSyntaxRootAsync(cancellationToken);
            var projectModel = await projectDocument.GetSemanticModelAsync(cancellationToken);
            if (projectRoot is null || projectModel is null)
                continue;

            var declarations = projectRoot.DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Where(candidate => SymbolEqualityComparer.Default.Equals(
                    projectModel.GetDeclaredSymbol(candidate, cancellationToken),
                    recordSymbol))
                .ToArray();
            var updatedRoot = projectRoot;
            foreach (var declaration in declarations)
            {
                var currentDeclaration = updatedRoot.FindNode(declaration.Span)
                    .AncestorsAndSelf()
                    .OfType<RecordDeclarationSyntax>()
                    .FirstOrDefault() ?? declaration;
                var updated = AddMissingFieldAttributes(
                    currentDeclaration,
                    recordSymbol,
                    projectModel,
                    cancellationToken);
                if (!ReferenceEquals(updated, currentDeclaration))
                    updatedRoot = updatedRoot.ReplaceNode(currentDeclaration, updated);
            }
            if (updatedRoot != projectRoot)
                solution = solution.WithDocumentSyntaxRoot(projectDocument.Id, updatedRoot);
        }

        return solution;
    }

    private static RecordDeclarationSyntax AddMissingFieldAttributes(
        RecordDeclarationSyntax declaration,
        INamedTypeSymbol recordSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var positionalDeclaration = declaration.ParameterList is not null
            ? declaration
            : recordSymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<RecordDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.ParameterList is not null);
        var parameters = positionalDeclaration?.ParameterList?.Parameters;
        if (parameters is null || parameters.Value.Count == 0)
            return declaration;

        var updated = declaration;
        var authoredPropertyNames = new HashSet<string>(recordSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .SelectMany(candidate => candidate.Members.OfType<PropertyDeclarationSyntax>())
            .Select(property => property.Identifier.ValueText)
            , StringComparer.Ordinal);
        foreach (var parameter in parameters.Value)
        {
            var propertySymbol = recordSymbol.GetMembers(parameter.Identifier.ValueText)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(property => !property.IsStatic);
            var directField = recordSymbol.GetMembers(parameter.Identifier.ValueText)
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field => !field.IsStatic);
            var backingField = propertySymbol is null
                ? directField
                : FindBackingField(recordSymbol, propertySymbol);
            if (backingField is null)
                continue;

            var property = updated.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(candidate =>
                    candidate.Identifier.ValueText == parameter.Identifier.ValueText);
            var managedReference = IsManagedReference(
                parameter.AttributeLists,
                model);
            var existingSerialization = GetFieldSerializationKind(backingField);
            if (managedReference && existingSerialization == FieldSerializationKind.SerializeReference)
                continue;
            if (!managedReference && existingSerialization != FieldSerializationKind.None)
                continue;

            if (managedReference && existingSerialization == FieldSerializationKind.SerializeField)
            {
                var replaced = ReplaceExistingSerializeField(
                    updated,
                    parameter.Identifier.ValueText,
                    model);
                if (!ReferenceEquals(replaced, updated))
                {
                    updated = replaced;
                    continue;
                }
            }

            var attribute = CreateFieldAttribute(
                managedReference
                    ? "global::UnityEngine.SerializeReference"
                    : "global::UnityEngine.SerializeField");
            if (property is not null)
            {
                updated = updated.ReplaceNode(
                    property,
                    property.WithAttributeLists(property.AttributeLists.Add(attribute)));
                continue;
            }

            var field = updated.Members
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Declaration.Variables.Any(variable =>
                    variable.Identifier.ValueText == parameter.Identifier.ValueText));
            if (field is not null && directField is not null)
            {
                var fieldAttribute = CreateDirectFieldAttribute(
                    managedReference
                        ? "global::UnityEngine.SerializeReference"
                        : "global::UnityEngine.SerializeField");
                updated = updated.ReplaceNode(
                    field,
                    field.WithAttributeLists(field.AttributeLists.Add(fieldAttribute)));
                continue;
            }

            if (!authoredPropertyNames.Contains(parameter.Identifier.ValueText) &&
                updated.ParameterList is not null)
            {
                var currentParameter = updated.ParameterList.Parameters
                    .FirstOrDefault(candidate =>
                        candidate.Identifier.ValueText == parameter.Identifier.ValueText);
                if (currentParameter is not null)
                {
                    updated = updated.ReplaceNode(
                        currentParameter,
                        currentParameter.WithAttributeLists(
                            currentParameter.AttributeLists.Add(attribute)));
                }
            }
        }

        return updated;
    }

    private static async Task<bool> CanApplyFieldFixAsync(
        Document document,
        Diagnostic diagnostic,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null || FindRecord(root, diagnostic) is not { } record)
            return false;
        var recordSymbol = model.GetDeclaredSymbol(record, cancellationToken) as INamedTypeSymbol;
        if (recordSymbol is null)
            return false;

        var positionalDeclaration = recordSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.ParameterList is not null);
        var parameters = positionalDeclaration?.ParameterList?.Parameters;
        if (parameters is null)
            return false;

        return parameters.Value.Any(parameter =>
        {
            var property = recordSymbol.GetMembers(parameter.Identifier.ValueText)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(candidate => !candidate.IsStatic);
            var directField = recordSymbol.GetMembers(parameter.Identifier.ValueText)
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field => !field.IsStatic);
            var field = property is not null
                ? FindBackingField(recordSymbol, property)
                : directField;
            if (field is null)
                return false;

            var managedReference = IsManagedReference(parameter.AttributeLists, model);
            var serializationKind = GetFieldSerializationKind(field);
            return managedReference
                ? serializationKind != FieldSerializationKind.SerializeReference
                : serializationKind == FieldSerializationKind.None;
        });
    }

    private static IFieldSymbol? FindBackingField(
        INamedTypeSymbol recordSymbol,
        IPropertySymbol property)
        => recordSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => SymbolEqualityComparer.Default.Equals(
                field.AssociatedSymbol,
                property));

    private static RecordDeclarationSyntax? FindRecord(
        SyntaxNode root,
        Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        return node.AncestorsAndSelf().OfType<RecordDeclarationSyntax>().FirstOrDefault();
    }

    private static AttributeListSyntax CreateFieldAttribute(string attributeName)
        => ParseAttributeList($"[field: {attributeName}]");

    private static AttributeListSyntax CreateDirectFieldAttribute(string attributeName)
        => ParseAttributeList($"[{attributeName}]");

    private static AttributeListSyntax ParseAttributeList(string text)
        => SyntaxFactory.ParseCompilationUnit(
                $"{text} class __SpireRecordVariantAttributeHost {{ }}")
            .DescendantNodes()
            .OfType<AttributeListSyntax>()
            .First();

    private static FieldSerializationKind GetFieldSerializationKind(IFieldSymbol field)
    {
        var attributes = field.GetAttributes();
        if (attributes.Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeReference"))
        {
            return FieldSerializationKind.SerializeReference;
        }

        return attributes.Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField")
            ? FieldSerializationKind.SerializeField
            : FieldSerializationKind.None;
    }

    private static RecordDeclarationSyntax ReplaceExistingSerializeField(
        RecordDeclarationSyntax declaration,
        string parameterName,
        SemanticModel model)
    {
        var updated = declaration;
        if (updated.ParameterList is not null)
        {
            var parameter = updated.ParameterList.Parameters.FirstOrDefault(candidate =>
                candidate.Identifier.ValueText == parameterName);
            if (parameter is not null)
            {
                var attributes = ReplaceExistingSerializeField(
                    parameter.AttributeLists,
                    model,
                    requireFieldTarget: true);
                if (attributes != parameter.AttributeLists)
                {
                    updated = updated.ReplaceNode(
                        parameter,
                        parameter.WithAttributeLists(attributes));
                }
            }
        }

        foreach (var property in updated.Members.OfType<PropertyDeclarationSyntax>()
                     .Where(candidate => candidate.Identifier.ValueText == parameterName)
                     .ToArray())
        {
            var attributes = ReplaceExistingSerializeField(
                property.AttributeLists,
                model,
                requireFieldTarget: true);
            if (attributes != property.AttributeLists)
            {
                updated = updated.ReplaceNode(
                    property,
                    property.WithAttributeLists(attributes));
            }
        }

        foreach (var field in updated.Members.OfType<FieldDeclarationSyntax>()
                     .Where(candidate => candidate.Declaration.Variables.Any(variable =>
                         variable.Identifier.ValueText == parameterName))
                     .ToArray())
        {
            var attributes = ReplaceExistingSerializeField(
                field.AttributeLists,
                model,
                requireFieldTarget: false);
            if (attributes != field.AttributeLists)
            {
                updated = updated.ReplaceNode(
                    field,
                    field.WithAttributeLists(attributes));
            }
        }

        return updated;
    }

    private static SyntaxList<AttributeListSyntax> ReplaceExistingSerializeField(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel model,
        bool requireFieldTarget)
    {
        var updated = attributeLists;
        foreach (var list in attributeLists)
        {
            if (requireFieldTarget && list.Target?.Identifier.ValueText != "field")
                continue;

            foreach (var attribute in list.Attributes)
            {
                if (!IsUnityAttribute(attribute, model, "SerializeField"))
                    continue;

                var replacement = attribute.WithName(
                    SyntaxFactory.ParseName("global::UnityEngine.SerializeReference"));
                var updatedList = list.ReplaceNode(attribute, replacement);
                return updated.Replace(list, updatedList);
            }
        }

        return updated;
    }

    private static bool IsUnityAttribute(
        AttributeSyntax attribute,
        SemanticModel model,
        string name)
    {
        var symbol = model.GetSymbolInfo(attribute).Symbol ??
            model.GetSymbolInfo(attribute.Name).Symbol;
        var attributeType = symbol switch
        {
            IMethodSymbol constructor => constructor.ContainingType,
            INamedTypeSymbol type => type,
            _ => null
        };
        return attributeType?.ToDisplayString() == $"UnityEngine.{name}";
    }

    private static bool IsManagedReference(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel model)
        => attributeLists.Any(list => list.Attributes.Any(attribute =>
        {
            var attributeModel = model.SyntaxTree == attribute.SyntaxTree
                ? model
                : model.Compilation.GetSemanticModel(attribute.SyntaxTree);
            var symbol = attributeModel.GetSymbolInfo(attribute).Symbol ??
                attributeModel.GetSymbolInfo(attribute.Name).Symbol;
            var attributeType = symbol switch
            {
                IMethodSymbol constructor => constructor.ContainingType,
                INamedTypeSymbol type => type,
                _ => null
            };
            return attributeType?.ToDisplayString() ==
                "Houtamelo.Spire.UnitySerializeReferenceAttribute";
        }));
}
