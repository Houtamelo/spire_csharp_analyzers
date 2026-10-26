using Houtamelo.Spire;

namespace Test
{
[DiscriminatedUnion]
public abstract partial record JobStatus
{
    public sealed partial record Running(int pid) : JobStatus;
    public sealed partial record Done(int exitCode) : JobStatus;
    public sealed partial record Pending(string jobName) : JobStatus;
}

class Consumer
{
    void Handle(JobStatus status)
    {
        switch (status)
        {
            case JobStatus.Running running:
                _ = running.pid;
                break;
            case JobStatus.Done done:
                _ = done.exitCode;
                break;
            case null:
                break;
        }
    }
}
}
