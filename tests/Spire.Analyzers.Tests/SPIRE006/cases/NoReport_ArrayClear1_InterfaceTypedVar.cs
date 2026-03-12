//@ should_pass
// Ensure that SPIRE006 is NOT triggered when the argument to Array.Clear is typed as an interface (non-array static type).
using System.Collections;

public class NoReport_ArrayClear1_InterfaceTypedVar
{
    public void Method()
    {
        IList list = new MustInitStruct[5];
        Array.Clear((Array)list);
    }
}
