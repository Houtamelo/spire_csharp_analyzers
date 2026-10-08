using System;
using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.Predefined
{
    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct PredefinedUnion
    {
        [Variant]
        public static partial PredefinedUnion Empty();

        [Variant]
        public static partial PredefinedUnion Value(int value);
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
            var union = PredefinedUnion.Value(42);
            return union.kind == PredefinedUnion.Kind.Value
                && union.value == 42
                && Initialized(42)[0].value == 42;
        }
        #pragma warning restore SPIRE014
    }
}
