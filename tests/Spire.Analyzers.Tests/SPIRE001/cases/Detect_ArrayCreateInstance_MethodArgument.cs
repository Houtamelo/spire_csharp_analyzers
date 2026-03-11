//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance as a method argument.
public class Detect_ArrayCreateInstance_MethodArgument
{
    public void Consume(Array arr) { }

    public void Method()
    {
        Consume(Array.CreateInstance(typeof(MustInitStruct), 5)); //~ ERROR
    }
}
