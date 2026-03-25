//@ should_pass
// Ensure that SPIRE001 is NOT triggered when array element type is nullable [EnforceInitialization] record.
#nullable enable
public class NoReport_NullableArrayRecord
{
    void Ok()
    {
        var arr = new EnforceInitializationRecord?[5];
    }
}
