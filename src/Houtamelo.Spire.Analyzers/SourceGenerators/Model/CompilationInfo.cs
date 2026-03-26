using System;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal sealed record CompilationInfo(
    bool HasSystemTextJson,
    bool HasNewtonsoftJson,
    bool AllowsUnsafe,
    bool HasInlineArray,
    bool HasInitProperties
) : IEquatable<CompilationInfo>;
