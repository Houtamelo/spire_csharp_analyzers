//@ should_fail
// Ensure that SPIRE016 IS triggered when an integer variable is cast to StatusNoZero inside a nested class method.
public class Detect_CastToEnum_Variable_InsideNestedClass
{
    class Inner
    {
        StatusNoZero Convert(int val)
        {
            return (StatusNoZero)val; //~ ERROR
        }

        void UseInLocal(int x)
        {
            StatusNoZero s = (StatusNoZero)x; //~ ERROR
        }
    }

    class DeepNested
    {
        class EvenDeeper
        {
            StatusNoZero Map(int code) => (StatusNoZero)code; //~ ERROR
        }
    }
}
