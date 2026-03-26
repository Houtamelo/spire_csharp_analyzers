//@ should_fail
// Ensure that SPIRE016 IS triggered when an integer variable is cast to StatusNoZero as a tuple element.
public class Detect_CastToEnum_Variable_TupleElement
{
    (StatusNoZero, string) GetTuple(int val)
    {
        return ((StatusNoZero)val, "label"); //~ ERROR
    }

    void Deconstruct(int rawValue)
    {
        var (status, code) = ((StatusNoZero)rawValue, rawValue.ToString()); //~ ERROR
    }
}
