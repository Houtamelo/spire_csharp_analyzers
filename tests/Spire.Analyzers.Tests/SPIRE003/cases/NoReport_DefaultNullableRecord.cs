//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default(T?) on a nullable [MustBeInit] record.
#nullable enable
public class NoReport_DefaultNullableRecord
{
    MustInitRecord? Ok() => default(MustInitRecord?);
}
