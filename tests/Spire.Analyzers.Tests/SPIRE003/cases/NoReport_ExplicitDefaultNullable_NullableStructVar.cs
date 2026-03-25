//@ should_pass
// default(EnforceInitializationStruct?) produces null — Nullable<T> does not have [EnforceInitialization]
public class NoReport_ExplicitDefaultNullable_NullableStructVar
{
    public void Method()
    {
        EnforceInitializationStruct? val = default(EnforceInitializationStruct?);
    }
}
