//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct containing only instance methods and no fields.
[EnforceInitialization] //~ ERROR
public struct MethodsOnlyStruct
{
    public int Compute() => 42;
    public void DoNothing() { }
}
