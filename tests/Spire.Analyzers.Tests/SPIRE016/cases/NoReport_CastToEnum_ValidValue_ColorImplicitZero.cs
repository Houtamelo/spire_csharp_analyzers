//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (ColorImplicitZero)0 is cast — 0 matches Red (implicit zero).
public class NoReport_CastToEnum_ValidValue_ColorImplicitZero
{
    public void Method()
    {
        ColorImplicitZero x = (ColorImplicitZero)0;
    }
}
