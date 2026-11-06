using Houtamelo.Spire;

[EnforceExhaustiveness]
enum Color
{
    Red,
    Green,
}

class Consumer
{
    int GetScore(Color? color) => color switch
    {
        Color.Red => 1,
        Color.Green => 2,
        null => throw new System.NotImplementedException(),
    };
}
