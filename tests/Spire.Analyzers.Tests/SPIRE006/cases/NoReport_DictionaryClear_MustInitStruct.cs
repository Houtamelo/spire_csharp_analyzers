//@ should_pass
// Ensure that SPIRE006 is NOT triggered when dict.Clear() is called on a Dictionary<int, MustInitStruct>.
public class NoReport_DictionaryClear_MustInitStruct
{
    public void Method()
    {
        var dict = new Dictionary<int, MustInitStruct>();
        dict[0] = new MustInitStruct(1);
        dict.Clear();
    }
}
