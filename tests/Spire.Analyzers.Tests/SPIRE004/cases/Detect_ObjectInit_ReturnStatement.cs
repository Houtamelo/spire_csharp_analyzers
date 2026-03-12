//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { }` is used in a return statement.
public class Detect_ObjectInit_ReturnStatement
{
    public MustInitNoCtor Method()
    {
        return new MustInitNoCtor { }; //~ ERROR
    }
}
