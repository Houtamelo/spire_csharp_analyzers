//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is on a class with instance fields.
[EnforceInitialization]
public class EnforceInitializationClassWithField
{
    public int Value;
}
