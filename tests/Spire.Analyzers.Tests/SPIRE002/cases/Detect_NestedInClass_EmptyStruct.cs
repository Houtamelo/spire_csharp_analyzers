//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct nested inside a class and that struct has no instance fields.
public class Outer
{
    [MustBeInit] //~ ERROR
    public struct Inner { }
}
