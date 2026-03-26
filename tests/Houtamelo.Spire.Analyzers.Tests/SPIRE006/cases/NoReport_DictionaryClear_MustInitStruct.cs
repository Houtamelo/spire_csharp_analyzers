//@ should_pass
// Ensure that SPIRE006 is NOT triggered when dict.Clear() is called on a Dictionary<int, EnforceInitializationStruct>.
public class NoReport_DictionaryClear_EnforceInitializationStruct
{
    public void Method()
    {
        var dict = new Dictionary<int, EnforceInitializationStruct>();
        dict[0] = new EnforceInitializationStruct(1);
        dict.Clear();
    }
}
