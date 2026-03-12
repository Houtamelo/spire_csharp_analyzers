//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { Value = 42, Name = "x" }` — object initializer with field assignments still calls the implicit parameterless ctor.
public class Detect_ObjectInit_WithFieldAssignments
{
    public void Method()
    {
        var x = new MustInitNoCtor { Value = 42, Name = "x" }; //~ ERROR
    }
}
