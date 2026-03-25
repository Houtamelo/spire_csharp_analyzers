//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a for loop body.
public class Detect_1DConstantSize_ForLoopBody
{
    public void Method()
    {
        for (int i = 0; i < 3; i++)
        {
            var arr = new EnforceInitializationStruct[5]; //~ ERROR
        }
    }
}
