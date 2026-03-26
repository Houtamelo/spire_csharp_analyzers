//@ should_fail
// Record union in switch statement missing Pending variant — SPIRE009
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record JobStatus
    {
        public sealed partial record Running(int pid) : JobStatus;
        public sealed partial record Done(int exitCode) : JobStatus;
        public sealed partial record Pending(string jobName) : JobStatus;
    }

    class JobStatusConsumer
    {
        void Handle(JobStatus status)
        {
            switch (status) //~ ERROR
            {
                case JobStatus.Running r:
                    System.Console.WriteLine($"running:{r.pid}");
                    break;
                case JobStatus.Done d:
                    System.Console.WriteLine($"done:{d.exitCode}");
                    break;
            }
        }
    }
}
