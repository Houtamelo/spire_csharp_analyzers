//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitPartialFieldsInitialized()` is returned from a method.
public class Detect_PartialFieldsInitialized_ReturnStatement
{
    public MustInitPartialFieldsInitialized Method()
    {
        return new MustInitPartialFieldsInitialized(); //~ ERROR
    }
}
