//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)99 is passed as a method argument.
public class Detect_CastToEnum_ConstantInvalidValue_MethodArgument
{
    public void Consume(StatusNoZero value) { }

    public void Method()
    {
        Consume((StatusNoZero)99); //~ ERROR
    }
}
