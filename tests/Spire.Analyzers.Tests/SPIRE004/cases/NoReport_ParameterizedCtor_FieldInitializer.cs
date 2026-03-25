//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is used as a field initializer for a [EnforceInitialization] struct.
public class NoReport_ParameterizedCtor_FieldInitializer
{
    private EnforceInitializationNoCtor _field = new EnforceInitializationNoCtor(42, "hello");
}
