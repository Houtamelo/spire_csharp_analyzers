//@ should_pass
// default(MustInitStruct?) produces null — Nullable<T> does not have [MustBeInit]
public class NoReport_ExplicitDefaultNullable_NullableStructVar
{
    public void Method()
    {
        MustInitStruct? val = default(MustInitStruct?);
    }
}
