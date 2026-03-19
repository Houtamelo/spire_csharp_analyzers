using Spire;

namespace Access
{
    [DiscriminatedUnion]
    internal partial record Outcome
    {
        public partial record Success(int Code) : Outcome;
        public partial record Failure(string Message) : Outcome;
    }
}
