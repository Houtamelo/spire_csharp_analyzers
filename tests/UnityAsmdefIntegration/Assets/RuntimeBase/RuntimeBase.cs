using System;
using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.RuntimeBase
{
    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct RuntimeBaseUnion
    {
        [Variant]
        public static partial RuntimeBaseUnion Empty();

        [Variant]
        public static partial RuntimeBaseUnion Value(int value);
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
            var union = RuntimeBaseUnion.Value(42);
            return union.kind == RuntimeBaseUnion.Kind.Value
                && union.value == 42
                && Initialized(42)[0].value == 42;
        }
        #pragma warning restore SPIRE014
    }
}
