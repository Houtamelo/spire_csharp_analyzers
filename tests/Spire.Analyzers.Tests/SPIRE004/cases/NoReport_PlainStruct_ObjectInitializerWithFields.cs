//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new PlainStruct { Value = 42 } is used — PlainStruct has no [MustBeInit].
public class NoReport_PlainStruct_ObjectInitializerWithFields
{
    public void Method()
    {
        var x = new PlainStruct { Value = 42 };
    }
}
