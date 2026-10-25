using System;
using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.EditorExplicit
{
    [Serializable]
    [DiscriminatedUnion]
    public partial struct EditorExplicitUnion
    {
        [Variant]
        public static partial EditorExplicitUnion Empty();

        [Variant]
        public static partial EditorExplicitUnion Value(int value);
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
            var union = EditorExplicitUnion.Value(42);
            return union.kind == EditorExplicitUnion.Kind.Value
                && union.value.value == 42
                && Initialized(42)[0].value == 42;
        }
        #pragma warning restore SPIRE014
    }
}
