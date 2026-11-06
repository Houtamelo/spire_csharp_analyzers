using Houtamelo.Spire;

[EnforceExhaustiveness]
enum Token
{
    @null = 0,
    Done = 1,
}

class Consumer
{
    int GetValue(Token token) => token switch
    {
        Token.Done => 1,
        Token.@null => throw new System.NotImplementedException(),
    };
}
