//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(PlainStruct)) is in an expression-bodied method.
public class NoReport_TypeOnly_ExpressionBodyMethod
{
    public object Method() => Activator.CreateInstance(typeof(PlainStruct));
}
