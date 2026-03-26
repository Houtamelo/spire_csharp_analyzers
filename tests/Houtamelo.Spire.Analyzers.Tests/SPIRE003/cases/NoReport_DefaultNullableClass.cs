//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default on a nullable [EnforceInitialization] class.
#nullable enable
public class NoReport_DefaultNullableClass
{
    EnforceInitializationClass? Ok() => default;
}
