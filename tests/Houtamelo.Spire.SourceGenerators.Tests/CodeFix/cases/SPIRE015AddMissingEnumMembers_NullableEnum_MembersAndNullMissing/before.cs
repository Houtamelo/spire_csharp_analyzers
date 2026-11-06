using Houtamelo.Spire;

[EnforceExhaustiveness]
enum Color
{
    Red,
    Green,
    Blue,
}

class Consumer
{
    int GetScore(Color? color) => color switch
    {
        Color.Red => 1,
    };
}
