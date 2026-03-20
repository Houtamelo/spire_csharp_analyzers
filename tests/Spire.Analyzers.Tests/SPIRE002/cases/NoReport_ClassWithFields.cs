//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is on a class with instance fields.
[MustBeInit]
public class MustInitClassWithField
{
    public int Value;
}
