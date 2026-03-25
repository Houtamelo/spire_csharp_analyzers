//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a struct nested inside a class and that struct has an instance field.
public class OuterClass
{
    [EnforceInitialization]
    public struct InnerWithField
    {
        public int Value;
    }
}
