//@ should_fail
// Ensure that SPIRE003 IS triggered when default is assigned to EnforceInitializationStruct inside a nested class.
public class Detect_DefaultLiteral_NestedInClass
{
    public class NestedClass
    {
        public void Method()
        {
            EnforceInitializationStruct s = default; //~ ERROR
        }
    }
}
