//@ should_fail
// Ensure that SPIRE006 IS triggered when memory.Span.Clear() is called where memory is Memory<MustInitStruct>.
public class Detect_SpanClear_ViaMemory
{
    public void Method()
    {
        Memory<MustInitStruct> memory = new MustInitStruct[5];
        memory.Span.Clear(); //~ ERROR
    }
}
