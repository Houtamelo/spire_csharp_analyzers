//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is used in a return statement.
public class Detect_TypeOnly_ReturnStatement
{
    public object Method()
    {
        return Activator.CreateInstance(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
