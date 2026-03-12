//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is assigned to an out parameter.
public class Detect_NewT_OutParameterAssignment
{
    public void Method(out MustInitNoCtor result)
    {
        result = new MustInitNoCtor(); //~ ERROR
    }
}
