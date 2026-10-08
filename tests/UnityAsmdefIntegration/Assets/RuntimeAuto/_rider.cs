using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.RuntimeAuto
{
    [EnforceInitialization]
    public enum ProbeKind
    {
        Empty = 1,
        Value = 2,
    }

    // SPIRE016: Rider should offer "Replace with SpireEnum<T>.From(value)".
    public static class RiderCodeFixProbe
    {
        public static ProbeKind Convert(int value)
        {
            return (ProbeKind)value;
        }
    }
}
