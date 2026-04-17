using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record HostParamInfo(
    string Name,
    string Type,
    int Position,
    string? RefKind,
    bool IsThis
) : IEquatable<HostParamInfo>;

internal sealed record InlinableHostDecl(
    string Namespace,
    string MethodName,
    string Accessibility,
    bool IsStatic,
    string ReturnType,
    string DeclaringTypeName,
    string DeclaringTypeKeyword,
    string DeclaringTypeAccessibility,
    EquatableArray<string> DeclaringTypeParameters,
    EquatableArray<InlinerContainingType> ContainingTypes,
    EquatableArray<string> HostTypeParameters,
    EquatableArray<string> HostTypeParameterConstraints,
    EquatableArray<InlinableParam> InlinableParams,
    EquatableArray<HostParamInfo> OtherParams,
    string OriginalBody,
    EquatableArray<string> UsingDirectives,
    InlinerDiagnostic? Diagnostic
) : IEquatable<InlinableHostDecl>;
