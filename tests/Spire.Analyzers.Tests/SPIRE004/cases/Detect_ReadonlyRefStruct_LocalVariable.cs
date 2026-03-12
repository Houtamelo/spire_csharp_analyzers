//@ should_fail
// Ensure that SPIRE004 IS triggered when `new` is used on a readonly ref [MustBeInit] struct without parameterless ctor.
public class Detect_ReadonlyRefStruct_LocalVariable
{
    public void Method()
    {
        var x = new MustInitReadonlyRefNoCtor(); //~ ERROR
    }
}
