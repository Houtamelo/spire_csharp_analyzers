//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() appears inside a lambda/delegate body.
public class Detect_SpanClear_InLambda
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        void Local()
        {
            Span<EnforceInitializationStruct> span = arr;
            span.Clear(); //~ ERROR
        }
        Local();
    }
}
