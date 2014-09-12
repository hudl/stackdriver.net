using log4net.Config;
using Topshelf;

namespace Hudl.StackDriver.PerfMon
{
    internal class Program
    {
        private static void Main()
        {
            BasicConfigurator.Configure();
            HostFactory.Run(x =>
            {
                x.Service<PerfMonService>(sc =>
                {
                    sc.ConstructUsing(() => new PerfMonService());
                    sc.WhenStarted(s => s.Start());
                    sc.WhenStopped(s => s.Stop());
                });
                x.SetServiceName("stackdriver-perfmon");
                x.SetDisplayName("Stackdriver Perfmon");
                x.SetDescription("Sends Perfmon Metrics to Stackdriver");
                x.StartAutomatically();
                x.RunAsPrompt();
                x.UseLog4Net();
            });
        }
    }
}


