//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default on a nullable [MustBeInit] class.
#nullable enable
public class NoReport_DefaultNullableClass
{
    MustInitClass? Ok() => default;
}
