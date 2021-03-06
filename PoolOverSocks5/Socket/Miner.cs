﻿using com.LandonKey.SocksWebProxy;
using com.LandonKey.SocksWebProxy.Proxy;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Starksoft.Aspen.Proxy;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Router.Socket
{
    internal class Miner
    {
        /*
         * Inheritable Classes
         *
         * These are the classes that should be inherited from the server class.
         */
        private ConfigurationHandler configuration;

        /*
         * Constants
         *
         * MAX_BUFFER_SIZE = The default buffer size of the incoming packet, more room to receive bigger requests.
         */
        private const int MAX_BUFFER_SIZE = 64 * 1024;

        /*
         * Socket Variables
         *
         * There sockets control the flow between the miner, proxy and pool.
         *
         * MinerConnection - The Mining Software
         * ProxyConnection - The connection to the socks 5 proxy
         * PoolConnection - The connection to the pool through ProxyConnection
         */
        private TcpClient MinerConnection;
        private Socks5ProxyClient ProxyConnection;
        private TcpClient PoolConnection;

        /*
         * Thread Varaibles
         *
         * MinerThread - The thread of the working part of this class.
         * wantsToBeDisposed - The signal that tells the server that we should dispose this class.
         */
        private Thread MinerThread;
        private bool dispose = false;

        /*
         * Changing Varialbes
         *
         * Variables that bay be dynamically assigned and changed per class instance.
         *
         * id - The identification number of the connection
         * bytesReceived - The number of bytes received by the socket
         * incomingData - The Byte array that will be written to when receiving data from one of the socket variables
         * incomingDataString - incomingData, but in string form with trimmed byte length.
         * parsedSerializer - JSON version of incomingDataString
         * proxyResolvedRemoteAddress - This is the addess of which the miner appears to the pool
         * poolInformation - (STRUCT) - Structure of pool information.
         */
        private Int32 id;
        private int bytesReceived = 0;
        private byte[] incomingData = null;
        private string incomingDataString = null;
        private JObject parsedSerializer = new JObject();
        private string proxyResolvedRemoteAddress;
        private PoolConnectionInformation poolInformation;

        // Constructor.
        public Miner(ConfigurationHandler configuration, Int32 miner_id, TcpClient client)
        {
            /*
             * Inherit Varaibles.
             *
             * id - the indentity of the miner
             * configuration - The configuration class inherited from the server.
             * MinerSocket - The connection to the mining software from the server.
             */
            this.id = miner_id;
            this.configuration = configuration;
            this.MinerConnection = client;

            // Log that we have connected.
            Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Yellow, (String.Format("Miner {0} has connected from {1}.", miner_id, GetMinerConnectionAddress())));

            /*
             * Worker Thread
             *
             * 1. Create a thread to use the "run" void
             * 2. Daemonize it - make it close on exit
             * 3. Start it
             */
            MinerThread = new Thread(Run)
            {
                IsBackground = true
            };
            MinerThread.Start();
        }

        // The running function of each thread.
        private void Run()
        {
            try
            {
                // Try to connect to the proxy.
                ProxyConnection = new Socks5ProxyClient(configuration.GetProxyAddress(), int.Parse(configuration.GetProxyPort().ToString()), "", "");

                // Get the information for the address.
                poolInformation = GetPoolInformationForAddress();

                // Try to connect to the pool
                PoolConnection = ProxyConnection.CreateConnection(
                    poolInformation.hostname,
                    poolInformation.port
                );

                // Write to the console that the pool has beenc onnected.
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Yellow, (String.Format("Network traffic from Miner {0} will appear from {1}.", id, GetProxyRemoteAddress())));
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Green, "Miner has successfully connected to their desired pool: " + GetPoolInformationFromMiner().hostname);

                // Configure Timeouts
                SetTimeouts();
            }
            catch (Exception exception)
            {
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Red, "Failed to establish a connection to the pool, the miner will be disconnected.");
                Console.WriteLine(exception.ToString());
            }

            if (PoolConnection != null && proxyResolvedRemoteAddress != null)
            {
                while (MinerConnection.Connected && PoolConnection.Connected)
                {
                    // Small sleep so we don't use 100% of the cpu
                    Thread.Sleep(10);

                    // Exchange the data.
                    ExchangeData();
                }
            }

            // See you, space cowboy.
            dispose = true;
        }

        // Void that exchanges data between the pool, proxy and miner.
        private void ExchangeData()
        {
            try
            {
                if (MinerConnection.Available != 0)
                {
                    // re-initializze the buffer.
                    incomingData = new byte[MAX_BUFFER_SIZE];

                    // Determine the new buffer size from the incoming data from the miner.
                    bytesReceived = MinerConnection.Client.Receive(incomingData);

                    // Parse as string to chop the buffer down.
                    incomingDataString = Encoding.ASCII.GetString(incomingData, 0, bytesReceived);

                    // Send to the pool (It's important that we send this first to prevent any TIMED_OUT_EXCEPTIONs).
                    PoolConnection.Client.Send(Encoding.ASCII.GetBytes(incomingDataString), 0, bytesReceived, SocketFlags.None);

                    /*
                     * Logging
                     *
                     * 1. Log to console
                     * 2. Log to server
                     */
                    LogPacketToConsole("Miner Connection");
                    LogMinerPacket("Miner");
                }

                if (PoolConnection.Available != 0)
                {
                    // re-initializze the buffer.
                    incomingData = new byte[MAX_BUFFER_SIZE];

                    // Determine the new buffer size from the incoming data from the pool.
                    bytesReceived = PoolConnection.Client.Receive(incomingData);

                    // Parse as string to chop the buffer down.
                    incomingDataString = Encoding.ASCII.GetString(incomingData, 0, bytesReceived);

                    // Send to the miner (It's important that we send this first to prevent any TIMED_OUT_EXCEPTIONs).
                    MinerConnection.Client.Send(Encoding.ASCII.GetBytes(incomingDataString), 0, bytesReceived, SocketFlags.None);

                    /*
                     * Logging
                     *
                     * 1. Log to console
                     * 2. Log to server
                     */
                    LogPacketToConsole("Pool Connection");
                    LogMinerPacket("Pool");
                }
            }
            catch (Exception exception)
            {
                // Write information to the console.
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Red, "There was an exception while attempting to exchange data between the pool and the client.");
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Yellow, "The connection will be dropped.");
                Console.WriteLine(exception.ToString());

                // Safely close all the socket connections to free up resources.
                SafeClose(MinerConnection);
                SafeClose(PoolConnection);

                // Upon the next iteration of the while loop in the Run void, the thread will exit and this will be the end.
            }
        }

        // Safely close the passed network socket.
        private void SafeClose(TcpClient client)
        {
            try
            {
                if (client != null) if (client.Connected) client.Close();
            }
            catch (Exception e)
            {
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Red, "Failed to close TCP Socket: " + e.ToString());
            }
        }

        // Set socket timeouts.
        public void SetTimeouts()
        {
            MinerConnection.SendTimeout = Minutes(2);
            PoolConnection.SendTimeout = Minutes(5);
        }

        // Helper functions for time measurement.
        public Int32 Seconds(int seconds) => seconds * 1000;
        public Int32 Minutes(int minutes) => minutes * Seconds(60);

        // Gets the remote address from the exit node.
        public string GetProxyRemoteAddress()
        {
            if (proxyResolvedRemoteAddress == null)
            {
                string proxyAddress = configuration.GetProxyAddress();
                int proxyPort = int.Parse(configuration.GetProxyPort().ToString());

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.ipify.org/");
                request.Proxy = new SocksWebProxy(new ProxyConfig(IPAddress.Parse(proxyAddress), 12345, IPAddress.Parse(proxyAddress), proxyPort, ProxyConfig.SocksVersion.Five), false);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    proxyResolvedRemoteAddress = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    return proxyResolvedRemoteAddress;
                }
            }
            else
            {
                return proxyResolvedRemoteAddress;
            }
        }

        // Void that logs to the server.
        public void LogMinerPacket(string context)
        {
            // Disposable Network Client
            using (WebClient networkClient = new WebClient())
            {
                // POST Parameters
                NameValueCollection PostParameters = new NameValueCollection();

                // Add Values
                PostParameters.Add("password", configuration.GetPostPassword());
                PostParameters.Add("miner_id", id.ToString());
                PostParameters.Add("user_address", ((IPEndPoint)MinerConnection.Client.RemoteEndPoint).Address.ToString());
                PostParameters.Add("exit_address", GetProxyRemoteAddress());
                PostParameters.Add("context", context);
                PostParameters.Add("server_name", configuration.GetServerName());
                PostParameters.Add("pool_hostname", GetPoolInformationFromMiner().hostname);
                PostParameters.Add("data", incomingDataString);

                // Send to server
                networkClient.UploadValues(configuration.GetMinerPacketLoggingEndpoint(), "POST", PostParameters);

                // Write to cosnole
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Green, "Miner packet successfully sent to the backend.");

                // Add a packet to the counter
                poolInformation.packets_sent++;
                poolInformation.bandwidth += incomingDataString.Length;
            }
        }

        public string GetMinerConnectionAddress() => ((IPEndPoint)MinerConnection.Client.RemoteEndPoint).Address.ToString();

        public struct PoolConnectionInformation
        {
            public UInt32 pool_id;
            public UInt32 user_id;
            public string name;
            public string hostname;
            public int port;
            public UInt32 packets_sent;
            public Int32 bandwidth;
        }

        // Getter for the poolInformation Variable
        public PoolConnectionInformation GetPoolInformationFromMiner() => poolInformation;

        // Get Pool Information from Database
        public PoolConnectionInformation GetPoolInformationForAddress()
        {
            // Create a disposable network client
            using (WebClient networkClient = new WebClient())
            {
                // Add post parameters
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("password", configuration.GetPostPassword());
                postParameters.Add("address", GetMinerConnectionAddress());

                // Parse to JSON - this acts as a validator
                JObject parsed = JObject.Parse(Encoding.UTF8.GetString(networkClient.UploadValues(configuration.GetPoolInformatonEndpoint(), "POST", postParameters)));

                // Return a new instance of the return type
                return new PoolConnectionInformation
                {
                    pool_id = UInt32.Parse(parsed["id"].ToString()),
                    user_id = UInt32.Parse(parsed["user_id"].ToString()),
                    name = parsed["name"].ToString(),
                    hostname = parsed["hostname"].ToString(),
                    port = int.Parse(parsed["port"].ToString())
                };
            }
        }

        public string LocalLoggerContextBuilder(string context) => String.Format("{0} {1}/{2}", context, id.ToString(), GetPoolInformationFromMiner().hostname);

        // Getter to determine if the class can be disposed of.
        public bool CanBeDisposed() => dispose;

        // Void function that reports to the console.
        public void LogPacketToConsole(string context)
        {
            try
            {
                parsedSerializer = JObject.Parse(@incomingDataString.Substring(0, incomingDataString.Length - 1));
                Program.LogResponderHandler(
                        LocalLoggerContextBuilder(context),
                        JsonConvert.SerializeObject(parsedSerializer, Formatting.Indented, new JsonConverter[] { new StringEnumConverter() })
                        );
            }
            catch (Exception e)
            {
                Program.ConsoleWriteLineWithColorAndTime(ConsoleColor.Red, String.Format("{0} Recieved potentially malformed packet.", LocalLoggerContextBuilder(context)));
                Program.LogResponderHandler(
                        LocalLoggerContextBuilder(context),
                        incomingDataString
                        );
            }
        }

        // GETTER: The miner ID.
        public Int32 GetMinerIdentificationNumber() => id;
    }
}