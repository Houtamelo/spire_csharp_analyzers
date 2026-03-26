//@ should_fail
// Ensure that SPIRE003 IS triggered when yield return default; is used in an IEnumerable<EnforceInitializationStruct> iterator.
public class Detect_DefaultLiteral_YieldReturn
{
    public IEnumerable<EnforceInitializationStruct> Method()
    {
        yield return default; //~ ERROR
    }
}
