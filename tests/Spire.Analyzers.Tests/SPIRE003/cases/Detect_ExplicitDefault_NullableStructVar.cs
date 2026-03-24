//@ should_fail
// default(MustInitStruct) assigned to MustInitStruct? — still a zeroed struct, not null
public class Detect_ExplicitDefault_NullableStructVar
{
    public void Method()
    {
        MustInitStruct? val = default(MustInitStruct); //~ ERROR
    }
}
