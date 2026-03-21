//@ should_fail
// Ensure that SPIRE015 IS triggered on both the outer and inner switch when both have incomplete Color coverage.
public class Detect_NestedSwitchStatements
{
    public void Method(Color outer, Color inner)
    {
        switch (outer) //~ ERROR
        {
            case Color.Red:
                switch (inner) //~ ERROR
                {
                    case Color.Green:
                        break;
                }
                break;
        }
    }
}
