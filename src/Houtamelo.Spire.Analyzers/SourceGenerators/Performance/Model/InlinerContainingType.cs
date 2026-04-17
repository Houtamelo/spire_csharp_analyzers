using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

/// A containing type in the nesting chain (outermost first). Unlike the
/// DU generator's ContainingTypeInfo, this carries generic type parameters
/// so the partial declaration can read "partial class Box&lt;T&gt;".
internal sealed record InlinerContainingType(
    string AccessibilityKeyword,
    string Keyword,
    string Name,
    EquatableArray<string> TypeParameters
) : IEquatable<InlinerContainingType>;
