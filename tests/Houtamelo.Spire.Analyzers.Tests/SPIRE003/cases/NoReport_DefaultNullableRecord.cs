//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default(T?) on a nullable [EnforceInitialization] record.
#nullable enable
public class NoReport_DefaultNullableRecord
{
    EnforceInitializationRecord? Ok() => default(EnforceInitializationRecord?);
}
