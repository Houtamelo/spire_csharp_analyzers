using System;

namespace Spire.SourceGenerators.Model;

internal sealed record VariantInfo(
    string Name,
    EquatableArray<FieldInfo> Fields
) : IEquatable<VariantInfo>;
