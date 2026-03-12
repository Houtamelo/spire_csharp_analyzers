//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is used in a return statement.
public class Detect_ExplicitDefault_ReturnStatement
{
    public MustInitStruct Method()
    {
        return default(MustInitStruct); //~ ERROR
    }
}
