//@ exhaustive
// Abstract intermediate — wildcard covers all
#nullable enable

[EnforceExhaustiveness]
public abstract class Tool { }
public abstract class HandTool : Tool { }
public sealed class Hammer : HandTool { }
public sealed class Screwdriver : HandTool { }
public sealed class Drill : Tool { }

public class AbstractIntermediate_Wildcard
{
    public int Test(Tool t) => t switch
    {
        Hammer => 1,
        _ => 2,
    };
}
