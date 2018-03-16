using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Router
{
    class ConfigurationHandler
    {
        public JObject loadedConfiguration;

        public ConfigurationHandler()
        {
            if (DoesConfigurationExist())
            {
                ReadConfiguration();
            }
            else
            {
                CreateNewConfiguration();
                WriteConfiguration();
            }
        }

        private void ReadConfiguration()
        {
            try
            {
                using (StreamReader file = File.OpenText(@GetConfigurationAbsolutePath()))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    loadedConfiguration = (JObject)JToken.ReadFrom(reader);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read configuration from the disk.");
                Console.WriteLine("Exception: " + ex.ToString());
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        private void WriteConfiguration()
        {
            try
            {
                // Write the configuration to disk.
                File.WriteAllText(GetConfigurationAbsolutePath(), JsonConvert.SerializeObject(loadedConfiguration, Formatting.Indented));

                // Let the user know.
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, (new String('=', Console.BufferWidth - 1)));
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "A previous configuration has not been found, as a result a new one has been written for you.");
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "Please change the settings and run 'dotnet run' again to start the relay.\n");
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "The configuration can be found at");
                Program.ConsoleWriteLineWithColor(ConsoleColor.Green, GetConfigurationAbsolutePath());

                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "\nPress any key to exit.");
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, (new String('=', Console.BufferWidth - 1)));
                Console.ReadLine();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write configuration to disk.");
                Console.WriteLine("Exception: " + ex.ToString());
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        private void CreateNewConfiguration()
        {
            JObject endpointConfiguration = new JObject
            {
                { "Base URL", "https://endpoint.com" },
                { "Password", "changeme" },
                { "Miner Packet Path", "/api/miner-packet" },
                { "Server Packet Path", "/api/server-packet" },
                { "Address To Pool Information", "/api/get-information" }
            };

            JObject newConfiguration = new JObject
            {
                // Pool Information
                { "Server Name", "United States - East Coast" },
                { "Server Broadcast", "us-east.endpoint.com:3333" },
                { "MySQL Hostname", "127.0.0.1" },
                { "MySQL Port", 3306 },
                { "MySQL Database ", "AnonymousPoolRouting" },
                { "MySQL Username", "router" },
                { "MySQL Password ", "" },
                { "Relay Address", "0.0.0.0" },
                { "Relay Port", 3333 },
                { "Proxy Address", "0.0.0.0" },
                { "Proxy Port", 3333 },
                { "Endpoint", endpointConfiguration },
            };

            loadedConfiguration = newConfiguration;
        }

        public string GetRelayAddress() => loadedConfiguration.GetValue("Relay Address").ToString();

        public uint GetRelayPort() => uint.Parse(loadedConfiguration.GetValue("Relay Port").ToString());

        public string GetProxyAddress() => loadedConfiguration.GetValue("Proxy Address").ToString();

        public uint GetProxyPort() => uint.Parse(loadedConfiguration.GetValue("Proxy Port").ToString());

        public string GetMySQLHostname() => loadedConfiguration.GetValue("MySQL Hostname").ToString();

        public string GetMySQLDatabase() => loadedConfiguration.GetValue("MySQL Database").ToString();

        public string GetMySQLUsername() => loadedConfiguration.GetValue("MySQL Username").ToString();

        public string GetMySQLPassword() => loadedConfiguration.GetValue("MySQL Password").ToString();

        public uint GetMySQLPort() => uint.Parse(loadedConfiguration.GetValue("MySQL Port").ToString());

        public string GetMinerPacketLoggingEndpoint() => String.Format(
            "{0}{1}", 
            loadedConfiguration["Endpoint"]["Base URL"].ToString(), 
            loadedConfiguration["Endpoint"]["Miner Packet Path"].ToString()
            );

        public string GetServerPacketLoggingEndpoint() => String.Format(
            "{0}{1}",
            loadedConfiguration["Endpoint"]["Base URL"].ToString(),
            loadedConfiguration["Endpoint"]["Server Packet Path"].ToString()
            );

        public string GetPoolInformatonEndpoint(string address) => String.Format(
            "{0}{1}/{2}",
            loadedConfiguration["Endpoint"]["Base URL"].ToString(),
            loadedConfiguration["Endpoint"]["Address To Pool Information"].ToString(),
            address
            );

        public string GetServerBroadcast() => loadedConfiguration.GetValue("Server Broadcast").ToString();

        public string GetServerName() => loadedConfiguration.GetValue("Server Name").ToString();

        public string GetPostPassword() => loadedConfiguration["Endpoint"]["Password"].ToString();

        public string GetConfigurationVersion() => typeof(RuntimeEnvironment).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

        private bool DoesConfigurationExist() => File.Exists(GetConfigurationAbsolutePath());

        public string GetConfigurationDirectory() => Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public string GetConfigurationAbsolutePath() => Path.Combine(GetConfigurationDirectory(), "config.json");
    }
}
