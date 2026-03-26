//@ should_fail
// Ensure that SPIRE004 IS triggered when `new` is used on a ref [EnforceInitialization] struct without parameterless ctor.
public class Detect_RefStruct_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationRefNoCtor(); //~ ERROR
    }
}
