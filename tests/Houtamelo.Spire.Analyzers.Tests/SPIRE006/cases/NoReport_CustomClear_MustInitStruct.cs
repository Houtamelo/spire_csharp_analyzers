//@ should_pass
// Ensure that SPIRE006 is NOT triggered when a user-defined void Clear() method is called on a custom type that holds EnforceInitializationStruct.
public class NoReport_CustomClear_EnforceInitializationStruct
{
    private class CustomContainer
    {
        private EnforceInitializationStruct[] _items = new EnforceInitializationStruct[4];

        public void Clear()
        {
            _items = new EnforceInitializationStruct[4];
        }
    }

    public void Method()
    {
        var container = new CustomContainer();
        container.Clear();
    }
}
