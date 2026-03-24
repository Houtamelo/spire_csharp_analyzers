//@ should_fail
// Ensure that SPIRE016 IS triggered when an integer variable is cast to StatusNoZero inside a foreach body.
public class Detect_CastToEnum_Variable_ForeachBody
{
    void Process(IEnumerable<int> values)
    {
        foreach (int rawInt in values)
        {
            StatusNoZero status = (StatusNoZero)rawInt; //~ ERROR
        }
    }

    List<StatusNoZero> Convert(IEnumerable<int> values)
    {
        var result = new List<StatusNoZero>();
        foreach (int rawInt in values)
        {
            result.Add((StatusNoZero)rawInt); //~ ERROR
        }
        return result;
    }
}
