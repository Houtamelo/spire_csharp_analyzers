//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is used in a return statement.
public class Detect_TypeOnly_ReturnStatement
{
    public object Method()
    {
        return Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
    }
}
