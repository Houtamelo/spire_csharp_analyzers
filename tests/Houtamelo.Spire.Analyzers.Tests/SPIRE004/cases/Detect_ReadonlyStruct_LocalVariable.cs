//@ should_fail
// Ensure that SPIRE004 IS triggered when `new` is used on a readonly [EnforceInitialization] struct without parameterless ctor.
public class Detect_ReadonlyStruct_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationReadonlyNoCtor(); //~ ERROR
    }
}
