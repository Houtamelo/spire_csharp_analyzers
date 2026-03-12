//@ should_pass
// Ensure that SPIRE006 is NOT triggered when a user-defined void Clear() method is called on a custom type that holds MustInitStruct.
public class NoReport_CustomClear_MustInitStruct
{
    private class CustomContainer
    {
        private MustInitStruct[] _items = new MustInitStruct[4];

        public void Clear()
        {
            _items = new MustInitStruct[4];
        }
    }

    public void Method()
    {
        var container = new CustomContainer();
        container.Clear();
    }
}
