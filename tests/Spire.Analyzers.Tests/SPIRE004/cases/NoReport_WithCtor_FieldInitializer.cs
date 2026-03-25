//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used as a field initializer and T has a user-defined parameterless ctor.
public class NoReport_WithCtor_FieldInitializer
{
    private EnforceInitializationWithCtor _field = new EnforceInitializationWithCtor();
}
