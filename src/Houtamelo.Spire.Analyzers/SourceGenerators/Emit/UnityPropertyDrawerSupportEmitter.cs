namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Emits the compilation-scoped Unity Editor implementation shared by all
/// generated discriminated-union property drawers.
internal static class UnityPropertyDrawerSupportEmitter
{
    internal const string HintName = "Spire.UnityDrawerSupport.generated.cs";

    internal static string Emit()
        => EmbeddedSourceTemplate.Read("UnityPropertyDrawerSupport.cs");
}
