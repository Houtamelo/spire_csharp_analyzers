namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

internal static class UnitySerializationSupportEmitter
{
    internal const string HintName = "Spire.UnitySerializationSupport.generated.cs";

    internal static string Emit()
        => EmbeddedSourceTemplate.Read("UnitySerializedPayload.cs");
}
