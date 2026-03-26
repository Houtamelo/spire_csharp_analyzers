//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is used in a static field initializer.
public class Detect_GenericOverload_FieldInitializer
{
    public static readonly EnforceInitializationStruct Field = Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
}
