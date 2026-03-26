//@ should_fail
// Ensure that SPIRE004 IS triggered when `new` is used on a readonly ref [EnforceInitialization] struct without parameterless ctor.
public class Detect_ReadonlyRefStruct_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationReadonlyRefNoCtor(); //~ ERROR
    }
}
