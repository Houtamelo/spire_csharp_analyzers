//@ should_fail
// Ensure that SPIRE016 IS triggered when an integer variable is cast to StatusNoZero inside a while loop body.
public class Detect_CastToEnum_Variable_WhileLoopBody
{
    void Process(int count)
    {
        int i = 0;
        while (i < count)
        {
            StatusNoZero s = (StatusNoZero)i; //~ ERROR
            i++;
        }
    }

    void DoWhileVariant(int limit)
    {
        int i = 1;
        do
        {
            StatusNoZero s = (StatusNoZero)i; //~ ERROR
            i++;
        } while (i < limit);
    }
}
