using Newtonsoft.Json.Linq;
using Router;
using Router.Socket;
using Starksoft.Aspen.Proxy;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Router
{
    class Server
    {
        /*
         * Inherited Variables
         * 
         * configuration - the configurationc lass from the main program.
         */
        private ConfigurationHandler configuration;

        /*
         * Miner Variables
         * 
         * ConnectedMiners - The list that holds all the class threads of the mienrs
         */
        private List<Miner> ConnectedMiners = new List<Miner>();

        /*
         * TCP Server
         * 
         * ServerConnection - the socket of the server
         */
        public TcpListener ServerConnection;

        /*
         * Reporter Thread
         * 
         * report - the variable that holds the thread.
         */
        private Thread reporter;

        /// <summary>
        /// Class Constructor
        /// 
        /// Inherts the configuration 
        /// </summary>
        /// <param name="configuration"></param>
        public Server(ConfigurationHandler configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Internal Worker
        /// 
        /// Starts listening and the reporter thread.
        /// </summary>
        public void Work()
        {
            try
            {
                // Create a new listener instance
                ServerConnection = new TcpListener(IPAddress.Parse(configuration.GetRelayAddress()), int.Parse(configuration.GetRelayPort().ToString()));

                // Start the TCP Listener
                ServerConnection.Start();

                // Enable the reporter as a background thread.
                reporter = new Thread(ReportThread)
                {
                    IsBackground = true
                };
                reporter.Start();

            } catch (Exception exception)
            {
                FailedToBindException(exception);
            }

            // Notify the console
            string pendingData = string.Format("Router started successfully: {0}:{1}\n", configuration.GetRelayAddress(), configuration.GetRelayPort());
            Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Green, pendingData);

            // Start listening for new clients and repeat.
            while (true)
            {
                // Wait for client connection
                TcpClient newClient = ServerConnection.AcceptTcpClient();

                // Cleanup old miners
                Cleanup();

                // Create a new miner
                Miner newMiner = new Miner(configuration, ConnectedMiners.Count + 1, newClient);

                // Keep track of it
                ConnectedMiners.Add(newMiner);

                // Write to the cosnole how many connections we have
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Yellow, String.Format("There are now {0} miner(s) connected.", GetMinerCount()));
            }
        }

        public void Cleanup() => ConnectedMiners = ConnectedMiners.Where(miner => !miner.CanBeDisposed()).ToList();

        public Int32 GetMinerCount() => ConnectedMiners.Count;

        // Thread that occasionally reports to the server.
        public void ReportThread()
        {
            while (true)
            {
                // Cleanup old miners
                Cleanup();

                // Log to backend
                LogServerStatistics();

                // Delay between reports.
                Thread.Sleep(configuration.GetUpdateFrequency() * 1000);
            }
        }

        // Compile all miner data
        public void LogServerStatistics()
        {
            // Placeholder JSON array
            JArray MinerDataPostArray = new JArray();

            // Iterate through each individual miner
            foreach (Miner miner in ConnectedMiners)
            {
                // Add the nested object to the placeholdder array
                MinerDataPostArray.Add(

                    // New JSON Object
                    new JObject
                    {
                        { "miner_id", miner.GetMinerIdentificationNumber().ToString() },
                        { "user_id", miner.GetPoolInformationFromMiner().user_id.ToString() },
                        { "pool_id", miner.GetPoolInformationFromMiner().pool_id.ToString() },
                        { "pool_name", miner.GetPoolInformationFromMiner().name },
                        { "pool_hostname", miner.GetPoolInformationFromMiner().hostname },
                        { "packets_sent", miner.GetPoolInformationFromMiner().packets_sent.ToString() },
                        { "bandwidth", miner.GetPoolInformationFromMiner().bandwidth.ToString() },
                        { "miner_address", miner.GetMinerConnectionAddress() },
                        { "exit_address", miner.GetProxyRemoteAddress() },
                    }
               );
            }

            // Try to send to server.
            try
            {
                // Disposable Network Client
                using (WebClient NetworkClient = new WebClient())
                {
                    // POST Parameters
                    NameValueCollection PostParameters = new NameValueCollection();

                    // Add Values
                    PostParameters.Add("password", configuration.GetPostPassword());
                    PostParameters.Add("server_name", configuration.GetServerName());
                    PostParameters.Add("server_broadcast_hostname", configuration.GetServerBroadcast());
                    PostParameters.Add("connections", GetMinerCount().ToString());
                    PostParameters.Add("miner_json", MinerDataPostArray.ToString());

                    // Post to server
                    NetworkClient.UploadValues(configuration.GetServerPacketLoggingEndpoint(), "POST", PostParameters);

                    // Write to console that it was sent.
                    Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Green, "Server statistic packet successfully sent to the backend.");

                }
            } catch (Exception e)
            {
                // Likely failed to post to server, we can ignore though.
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Red, "An exception occured while attempting to update the backend with server statistics.");
                Console.WriteLine(e.ToString());
            } 
        }

        // Failed to bind exception thrower.
        public void FailedToBindException(Exception exception)
        {
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, (new String('=', Console.BufferWidth - 1)));
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, string.Format("Failed to bind relay to {0}:{1}.\n", configuration.GetRelayAddress(), configuration.GetRelayPort()));
            Console.WriteLine(exception.ToString());
            Console.WriteLine("\nPress enter key to exit.");
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, (new String('=', Console.BufferWidth - 1)));
            Console.ReadLine();
            Environment.Exit(1);
        }
    }    
}