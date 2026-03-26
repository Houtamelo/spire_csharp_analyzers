//@ should_fail
// Ensure that SPIRE006 IS triggered when memory.Span.Clear() is called where memory is Memory<EnforceInitializationStruct>.
public class Detect_SpanClear_ViaMemory
{
    public void Method()
    {
        Memory<EnforceInitializationStruct> memory = new EnforceInitializationStruct[5];
        memory.Span.Clear(); //~ ERROR
    }
}
