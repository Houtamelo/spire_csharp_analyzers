//@ not_exhaustive
// bool? missing null case
public class NullableBoolMissingNull
{
    public int Test(bool? b) => b switch
    {
        true => 1,
        false => 2,
        //~ null
    };
}
