//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance in a return statement.
public class Detect_ArrayCreateInstance_ReturnStatement
{
    public Array Method()
    {
        return Array.CreateInstance(typeof(EnforceInitializationStruct), 5); //~ ERROR
    }
}
