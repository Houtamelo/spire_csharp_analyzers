//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct nested inside a class and that struct has no instance fields.
public class Outer
{
    [EnforceInitialization] //~ ERROR
    public struct Inner { }
}
