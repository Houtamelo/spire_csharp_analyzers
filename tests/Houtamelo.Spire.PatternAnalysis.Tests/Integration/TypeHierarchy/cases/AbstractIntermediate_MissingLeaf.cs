//@ not_exhaustive
// Abstract intermediate — missing one concrete leaf
#nullable enable

[EnforceExhaustiveness]
public abstract class Furniture { }
public abstract class Seating : Furniture { }
public sealed class Chair : Seating { }
public sealed class Sofa : Seating { }
public sealed class Table : Furniture { }

public class AbstractIntermediate_MissingLeaf
{
    public int Test(Furniture f) => f switch
    {
        Chair => 1,
        Sofa => 2,
        //~ Table
    };
}
