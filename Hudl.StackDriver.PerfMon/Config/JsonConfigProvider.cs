using System.IO;
using ServiceStack.Text;

namespace Hudl.StackDriver.PerfMon.Config
{
    internal class JsonConfigProvider
    {
        public static CountersConfig GetConfigFromFile(string filePath)
        {
            var configString = File.ReadAllText(filePath);
            var config = new JsonSerializer<CountersConfig>().DeserializeFromString(configString);
            return config;
        }
    }
}