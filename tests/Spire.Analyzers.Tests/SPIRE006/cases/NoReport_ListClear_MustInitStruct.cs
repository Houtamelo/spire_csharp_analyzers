//@ should_pass
// Ensure that SPIRE006 is NOT triggered when list.Clear() is called on a List<EnforceInitializationStruct>.
public class NoReport_ListClear_EnforceInitializationStruct
{
    public void Method()
    {
        var list = new List<EnforceInitializationStruct>();
        list.Add(new EnforceInitializationStruct(1));
        list.Clear();
    }
}
