//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a struct nested inside a class and that struct has an instance field.
public class OuterClass
{
    [MustBeInit]
    public struct InnerWithField
    {
        public int Value;
    }
}
