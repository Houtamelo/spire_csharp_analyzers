//@ should_fail
// Ensure that SPIRE016 IS triggered when the loop variable i is cast to StatusNoZero inside a for loop.
public class Detect_CastToEnum_Variable_ForLoop
{
    public void Method()
    {
        var results = new List<StatusNoZero>();
        for (int i = 1; i <= 3; i++)
        {
            results.Add((StatusNoZero)i); //~ ERROR
        }
    }
}
