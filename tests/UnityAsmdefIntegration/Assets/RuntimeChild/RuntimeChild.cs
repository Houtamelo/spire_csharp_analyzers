using System;
using Houtamelo.Spire;
using Spire.UnityAsmdefIntegration.RuntimeBase;

namespace Spire.UnityAsmdefIntegration.RuntimeChild
{
    [Serializable]
    [DiscriminatedUnion]
    public partial struct RuntimeChildUnion
    {
        [Variant]
        public static partial RuntimeChildUnion Empty();

        [Variant]
        public static partial RuntimeChildUnion Value(int value);
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
            var union = RuntimeChildUnion.Value(42);
            return union.kind == RuntimeChildUnion.Kind.Value
                && union.value.value == 42
                && Initialized(42)[0].value == 42
                && RuntimeBase.AsmdefProbe.Verify();
        }
        #pragma warning restore SPIRE014
    }
}
