using System;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record InlinerDiagnostic(
    string Id,
    string Message,
    string FilePath,
    int StartOffset,
    int Length,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn
) : IEquatable<InlinerDiagnostic>;
