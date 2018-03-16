using Router;
using Router.Socket;
using Starksoft.Aspen.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Router
{
    class Server
    {
        // Class Inheritance
        private ConfigurationHandler configuration;
        private Database database;

        // Miner Connections
        private List<Miner> ConnectedMiners;

        // TCP Socket for the whole class
        public TcpListener ServerConnection;

        private Thread reporter;

        // Class constructor
        public Server(ConfigurationHandler configuration, Database database)
        {
            // Initialize the placeholder variables;
            ConnectedMiners = new List<Miner>();

            // Inherit from the main class.
            this.configuration = configuration;
            this.database = database;

            // Enable the reporter
            reporter = new Thread(ReportThread);
            reporter.Start();
        }

        // Worker - The heart of the application.
        public void Work()
        {
            try
            {
                // Create a new listener instance
                ServerConnection = new TcpListener(IPAddress.Parse(configuration.GetRelayAddress()), int.Parse(configuration.GetRelayPort().ToString()));

                // Start the TCP Listener
                ServerConnection.Start();
            } catch (Exception exception)
            {
                FailedToBindException(exception);
            }

            // Notify the console
            string pendingData = string.Format("Please connect your miners to {0}:{1} to get started.\n", configuration.GetRelayAddress(), configuration.GetRelayPort());
            Program.ConsoleWriteLineWithColor(ConsoleColor.Green, pendingData);

            // Start listening for new clients and repeat.
            while (true)
            {
                // Wait for client connection
                TcpClient newClient = ServerConnection.AcceptTcpClient();

                // Cleanup old miners
                ConnectedMiners = ConnectedMiners.Where(miner => !miner.wantsToBeDisposed).ToList();

                // Create a new miner
                Miner newMiner = new Miner(configuration, database, ConnectedMiners.Count + 1, newClient);

                // Keep track of it
                ConnectedMiners.Add(newMiner);

                // Write to the cosnole how many connections we have
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, String.Format("There are now {0} miner(s) connected.", GetMinerCount()));
            }
        }

        public Int32 GetMinerCount() => ConnectedMiners.Count;

        public void ReportThread()
        {
            while (true)
            {
                BackendLogger.LogConnectionCount(configuration, this);
                Thread.Sleep(60 * 1000);
            }
        }

        public void FailedToBindException(Exception exception)
        {
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, (new String('=', Console.BufferWidth - 1)));
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, string.Format("Failed to bind relay to {0}:{1}.\n", configuration.GetRelayAddress(), configuration.GetRelayPort()));
            Console.WriteLine(exception.ToString());
            Console.WriteLine("\nPress any key to exit.");
            Program.ConsoleWriteLineWithColor(ConsoleColor.Red, (new String('=', Console.BufferWidth - 1)));
            Console.ReadLine();
            Environment.Exit(1);
        }
    }    
}