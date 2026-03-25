//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is assigned to an out parameter.
public class Detect_NewT_OutParameterAssignment
{
    public void Method(out EnforceInitializationNoCtor result)
    {
        result = new EnforceInitializationNoCtor(); //~ ERROR
    }
}
