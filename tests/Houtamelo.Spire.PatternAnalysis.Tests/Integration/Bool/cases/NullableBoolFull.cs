//@ exhaustive
// bool? with null + true + false
public class NullableBoolFull
{
    public int Test(bool? b) => b switch
    {
        null => 0,
        true => 1,
        false => 2,
    };
}
