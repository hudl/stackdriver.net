using System;
using System.IO;
using System.Reflection;
using Hudl.StackDriver.PerfMon.Config;
using log4net;

namespace Hudl.StackDriver.PerfMon
{
    internal class PerfMonService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (PerfMonService));
        private PerfMonReporter _reporter;

        public void Start()
        {
            const string configFileName = "config.json";

            var applicationFilePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (applicationFilePath == null)
            {
                Log.Error("Unable to get applications path");
                Environment.Exit(404);
            }

            var configfilePath = Path.Combine(applicationFilePath, configFileName);

            var config = JsonConfigProvider.GetConfigFromFile(configfilePath);

            _reporter = PerfMonAgentFactory.CreateAgentWithConfiguration(config);
            if (_reporter == null)
            {
                Log.Error("Unable to start service.");
                Environment.Exit(500);
            }

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
            if (_reporter != null)
            {
                _reporter.Stop();
            }
            Log.Info("Stopping service");

#if (DEBUG)
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
#endif
        }
    }
}