using System;
using Houtamelo.Spire;
using Spire.UnityAsmdefIntegration.RuntimeAuto;

namespace Spire.UnityAsmdefIntegration.EditorRuntime
{
    [Serializable]
    [DiscriminatedUnion]
    public partial struct EditorRuntimeUnion
    {
        [Variant]
        public static partial EditorRuntimeUnion Empty();

        [Variant]
        public static partial EditorRuntimeUnion Value(int value);
    }

    [EnforceInitialization]
    public struct InitializationProbe
    {
        public int value;

        public InitializationProbe(int value)
        {
            this.value = value;
        }
    }

    [EnforceInitialization]
    public struct WarningOnlyType
    {
    }

    public static class AsmdefProbe
    {
        public static InitializationProbe[] Initialized(int value)
        {
            return new InitializationProbe[]
            {
                new InitializationProbe(value),
            };
        }

        #pragma warning disable SPIRE014
        public static bool Verify()
        {
            var union = EditorRuntimeUnion.Value(42);
            return union.kind == EditorRuntimeUnion.Kind.Value
                && union.value.value == 42
                && Initialized(42)[0].value == 42
                && RuntimeAuto.AsmdefProbe.Verify();
        }
        #pragma warning restore SPIRE014
    }
}
