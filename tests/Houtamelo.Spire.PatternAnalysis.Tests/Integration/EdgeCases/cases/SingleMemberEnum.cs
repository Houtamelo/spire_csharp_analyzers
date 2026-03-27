//@ exhaustive
// Single-member enum trivially exhaustive
public class SingleMemberEnum
{
    public int Test(OneCase c) => c switch
    {
        OneCase.Only => 1,
    };
}
