using System;

namespace Spire.SourceGenerators.Model;

internal sealed record CompilationInfo(
    bool HasSystemTextJson,
    bool HasNewtonsoftJson
) : IEquatable<CompilationInfo>;
