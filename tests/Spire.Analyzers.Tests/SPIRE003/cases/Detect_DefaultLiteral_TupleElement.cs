//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used as a MustInitStruct element in a tuple literal.
public class Detect_DefaultLiteral_TupleElement
{
    public void Method()
    {
        (int, MustInitStruct) t = (1, default); //~ ERROR
    }
}
