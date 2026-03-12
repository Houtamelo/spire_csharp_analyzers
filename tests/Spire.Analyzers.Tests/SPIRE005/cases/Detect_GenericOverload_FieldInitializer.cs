//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is used in a static field initializer.
public class Detect_GenericOverload_FieldInitializer
{
    public static readonly MustInitStruct Field = Activator.CreateInstance<MustInitStruct>(); //~ ERROR
}
