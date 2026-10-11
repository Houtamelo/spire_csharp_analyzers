#nullable enable

namespace Spire.UnityIntegration
{
    public enum UnityIntegrationFixtureProfile
    {
        Full,
        BaselineOnly,
        ManagedReferenceOnly,
        ManagedReferenceNullOnly,
        ManagedReferenceSharedOnly,
        ManagedReferenceCycleOnly,
        MainBaselineAndManaged,
        StabilityBaselineAndManaged,
    }
}
