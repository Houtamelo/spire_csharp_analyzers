//@ should_fail
// default(EnforceInitializationStruct) assigned to EnforceInitializationStruct? — still a zeroed struct, not null
public class Detect_ExplicitDefault_NullableStructVar
{
    public void Method()
    {
        EnforceInitializationStruct? val = default(EnforceInitializationStruct); //~ ERROR
    }
}
