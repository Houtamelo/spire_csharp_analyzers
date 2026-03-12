//@ should_pass
// Ensure that SPIRE006 is NOT triggered when list.Clear() is called on a List<MustInitStruct>.
public class NoReport_ListClear_MustInitStruct
{
    public void Method()
    {
        var list = new List<MustInitStruct>();
        list.Add(new MustInitStruct(1));
        list.Clear();
    }
}
