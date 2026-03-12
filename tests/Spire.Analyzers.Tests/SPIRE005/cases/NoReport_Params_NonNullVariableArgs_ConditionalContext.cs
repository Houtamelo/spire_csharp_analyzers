//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance args come from a conditional expression and are not determinably null or empty.
public class NoReport_Params_NonNullVariableArgs_ConditionalContext
{
    public object Method(bool condition)
    {
        var args = condition ? new object[] { 1 } : new object[] { 2 };
        return Activator.CreateInstance(typeof(MustInitStruct), args);
    }
}
