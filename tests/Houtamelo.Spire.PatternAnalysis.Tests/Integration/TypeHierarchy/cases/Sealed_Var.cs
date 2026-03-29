//@ exhaustive
// Sealed class covered by var pattern
#nullable enable

[EnforceExhaustiveness]
public sealed class Marker { }

public class Sealed_Var
{
    public int Test(Marker m) => m switch
    {
        var x => 1,
    };
}
