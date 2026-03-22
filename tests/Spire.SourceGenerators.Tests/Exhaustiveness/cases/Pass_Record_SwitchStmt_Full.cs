//@ should_pass
// Record union switch statement all variants covered — no diagnostic
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record TaskState
    {
        public sealed partial record Queued(string taskName) : TaskState;
        public sealed partial record InProgress(int taskPid) : TaskState;
        public sealed partial record Completed(int taskExit) : TaskState;
    }

    class PassTaskStateConsumer
    {
        void Handle(TaskState state)
        {
            switch (state)
            {
                case TaskState.Queued q:
                    System.Console.WriteLine($"queued:{q.taskName}");
                    break;
                case TaskState.InProgress p:
                    System.Console.WriteLine($"running:{p.taskPid}");
                    break;
                case TaskState.Completed c:
                    System.Console.WriteLine($"done:{c.taskExit}");
                    break;
            }
        }
    }
}
