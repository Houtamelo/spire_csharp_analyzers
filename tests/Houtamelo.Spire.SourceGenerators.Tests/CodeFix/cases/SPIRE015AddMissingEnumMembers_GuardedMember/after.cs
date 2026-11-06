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
    static bool AllowBlue() => false;

    int GetScore(Color color) => color switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue when AllowBlue() => 3,
        Color.Blue => throw new System.NotImplementedException(),
    };
}
