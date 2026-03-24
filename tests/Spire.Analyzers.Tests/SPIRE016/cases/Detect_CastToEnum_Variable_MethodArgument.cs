//@ should_fail
// Ensure that SPIRE016 IS triggered when a variable is cast to StatusNoZero and passed as a method argument.
public class Detect_CastToEnum_Variable_MethodArgument
{
    public void Consume(StatusNoZero value) { }

    public void Method(int someVar)
    {
        Consume((StatusNoZero)someVar); //~ ERROR
    }
}
