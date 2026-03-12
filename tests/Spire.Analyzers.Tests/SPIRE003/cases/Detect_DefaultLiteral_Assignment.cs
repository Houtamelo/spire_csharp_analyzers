//@ should_fail
// Ensure that SPIRE003 IS triggered when default is assigned to an existing MustInitStruct variable (standalone assignment).
public class Detect_DefaultLiteral_Assignment
{
    public void Method()
    {
        MustInitStruct c = new MustInitStruct(1);
        c = default; //~ ERROR
    }
}
