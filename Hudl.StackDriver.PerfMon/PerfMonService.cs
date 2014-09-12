using System;
using log4net;

namespace Hudl.StackDriver.PerfMon
{
    internal class PerfMonService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PerfMonService));
        private PerfMonReporter _reporter;

        public void Start()
        {
            var factory = new PerfMonAgentFactory();
            _reporter = factory.CreateAgentWithConfiguration(JsonConfigProvider.GetConfigFromFile(@"C:\Users\jamie.snell\Documents\HudlGit\stackdriver.net\Hudl.StackDriver.PerfMon\config.json"));

            Log.Info("Starting service.");

            try
            {
                _reporter.Start();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start, unable to continue.", ex);
            }
        }

        public void Stop()
        {
            _reporter.Stop();
            Log.Info("Stopping service");
            Console.ReadLine();
        }
    }
}