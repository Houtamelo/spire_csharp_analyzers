//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is used inside a nested class.
public class Detect_TypeOnly_NestedInClass
{
    public class Inner
    {
        public void Method()
        {
            var x = Activator.CreateInstance(typeof(EnforceInitializationStruct)); //~ ERROR
        }
    }
}
