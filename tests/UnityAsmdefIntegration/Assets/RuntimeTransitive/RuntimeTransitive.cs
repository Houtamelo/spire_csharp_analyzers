using System;
using Houtamelo.Spire;
using Spire.UnityAsmdefIntegration.RuntimeChild;

namespace Spire.UnityAsmdefIntegration.RuntimeTransitive
{
    [Serializable]
    [DiscriminatedUnion]
    public partial struct RuntimeTransitiveUnion
    {
        [Variant]
        public static partial RuntimeTransitiveUnion Empty();

        [Variant]
        public static partial RuntimeTransitiveUnion Value(int value);
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
            var union = RuntimeTransitiveUnion.Value(42);
            return union.kind == RuntimeTransitiveUnion.Kind.Value
                && union.value.value == 42
                && Initialized(42)[0].value == 42
                && RuntimeChild.AsmdefProbe.Verify();
        }
        #pragma warning restore SPIRE014
    }
}
