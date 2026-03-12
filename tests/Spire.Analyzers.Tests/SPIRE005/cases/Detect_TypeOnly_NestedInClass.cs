//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is used inside a nested class.
public class Detect_TypeOnly_NestedInClass
{
    public class Inner
    {
        public void Method()
        {
            var x = Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
        }
    }
}
