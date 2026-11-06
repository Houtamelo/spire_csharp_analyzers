using Houtamelo.Spire;

[EnforceExhaustiveness]
enum AliasedEnum
{
    First = 0,
    AlsoFirst = 0,
    Second = 1,
}

class Consumer
{
    int GetValue(AliasedEnum value) => value switch
    {
        AliasedEnum.Second => 2,
        AliasedEnum.First => throw new System.NotImplementedException(),
    };
}
