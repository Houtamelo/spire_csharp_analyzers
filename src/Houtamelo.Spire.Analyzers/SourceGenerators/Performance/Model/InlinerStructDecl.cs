using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal readonly record struct InlinerParamInfo(
    string Name,
    string Type
) : IEquatable<InlinerParamInfo>;

internal sealed record InlinerStructDecl(
    string Namespace,
    string MethodName,
    string Accessibility,
    bool IsStatic,
    bool IsVoid,
    string? ReturnType,
    string DeclaringTypeName,
    string DeclaringTypeKeyword,
    string DeclaringTypeAccessibility,
    EquatableArray<string> DeclaringTypeParameters,
    EquatableArray<InlinerContainingType> ContainingTypes,
    EquatableArray<string> TypeParameters,
    EquatableArray<string> TypeParameterConstraints,
    EquatableArray<InlinerParamInfo> Parameters,
    InlinerDiagnostic? Diagnostic
) : IEquatable<InlinerStructDecl>;
