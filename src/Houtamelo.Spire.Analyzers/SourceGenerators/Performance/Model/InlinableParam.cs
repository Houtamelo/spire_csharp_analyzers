using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record InlinableParam(
    string Name,
    int Position,
    bool IsNullable,
    bool IsFunc,
    EquatableArray<string> DelegateTypeArguments,
    string ReturnType,
    string GenericName
) : IEquatable<InlinableParam>;
