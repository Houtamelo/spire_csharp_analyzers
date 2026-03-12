//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears in a method of a nested class.
public class Detect_SpanClear_InNestedType
{
    public class Inner
    {
        public void Method()
        {
            var arr = new MustInitStruct[5];
            Span<MustInitStruct> span = arr;
            span.Clear(); //~ ERROR
        }
    }
}
