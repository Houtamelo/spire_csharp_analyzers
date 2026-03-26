//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears in a method of a nested class.
public class Detect_ArrayClear1_InNestedType
{
    public class Inner
    {
        public void Method()
        {
            var arr = new EnforceInitializationStruct[5];
            Array.Clear(arr); //~ ERROR
        }
    }
}
