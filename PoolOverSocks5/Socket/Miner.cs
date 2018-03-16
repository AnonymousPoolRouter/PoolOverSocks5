using com.LandonKey.SocksWebProxy;
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
        private const int MAX_BUFFER_SIZE = 4096;        

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

        /// <summary>
        /// Constructor
        /// 
        /// The constructor is the function that gets called when instantiating a new class.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="miner_id"></param>
        /// <param name="client"></param>
        public Miner(ConfigurationHandler configuration, Int32 miner_id, TcpClient client)
        {
            // Let the console know a miner is attempting to connect.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(String.Format("Miner ID {0} has connected.", miner_id));
            Console.ResetColor();

            // Remember our assigned ID.
            this.id = miner_id;

            /*
             * Inherit Varaibles.
             * 
             * configuration - The configuration class inherited from the server.
             * MinerSocket - The connection to the mining software from the server.
             */
            this.configuration = configuration;
            this.MinerConnection = client;

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

        /// <summary>
        /// Run Void
        /// 
        /// The function that handles all the initial working and looping of exchanging data
        /// </summary>
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
                Program.ConsoleWriteLineWithColor(ConsoleColor.Green, "Miner has successfully connected to their desired pool: " + GetPoolInformationFromMiner().hostname);

                // Configure Timeouts
                SetTimeouts();
            }
            catch (Exception exception)
            {
                Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "Failed to establish a connection to the pool, the miner will be disconnected.");
                Console.WriteLine(exception.ToString());
            }

            if (PoolConnection != null) {
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

        /// <summary>
        /// Exchange Data
        /// 
        /// Exchanges data between all the sockets.
        /// </summary>
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

                    // Parse as string to chop the buffer down and parse json.
                    incomingDataString = Encoding.ASCII.GetString(incomingData, 0, bytesReceived);
                    parsedSerializer = JObject.Parse(@incomingDataString.Substring(0, incomingDataString.Length - 1));

                    // Log to the console what we have.
                    Program.LogResponderHandler(LocalLoggerContextBuilder("Miner Connection"), JsonConvert.SerializeObject(parsedSerializer, Formatting.Indented, new JsonConverter[] { new StringEnumConverter() }));

                    // Send to the pool (It's important that we send this first to prevent any TIMED_OUT_EXCEPTIONs).
                    PoolConnection.Client.Send(Encoding.ASCII.GetBytes(incomingDataString), 0, bytesReceived, SocketFlags.None);

                    // Log to the panel
                    LogMinerPacket("Miner", incomingDataString);

                }

                if (PoolConnection.Available != 0)
                {
                    // re-initializze the buffer.
                    incomingData = new byte[MAX_BUFFER_SIZE];

                    // Determine the new buffer size from the incoming data from the pool.
                    bytesReceived = PoolConnection.Client.Receive(incomingData);

                    // Parse as string to chop the buffer down and parse json.
                    incomingDataString = Encoding.ASCII.GetString(incomingData, 0, bytesReceived);
                    parsedSerializer = JObject.Parse(@incomingDataString.Substring(0, incomingDataString.Length - 1));

                    // Log to the console what we have.
                    Program.LogResponderHandler(LocalLoggerContextBuilder("Pool Connection"), JsonConvert.SerializeObject(parsedSerializer, Formatting.Indented, new JsonConverter[] { new StringEnumConverter() }));

                    // Send to the miner (It's important that we send this first to prevent any TIMED_OUT_EXCEPTIONs).
                    MinerConnection.Client.Send(Encoding.ASCII.GetBytes(incomingDataString), 0, bytesReceived, SocketFlags.None);

                    // Log to the panel 
                    LogMinerPacket("Pool", incomingDataString);

                }
            }
            catch (Exception exception)
            {
                // Write information to the console.
                Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "There was an exception while attempting to exchange data between the pool and the client.");
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "The connection will be dropped.");
                Console.WriteLine(exception.ToString());

                // Safely close all the socket connections to free up resources.
                SafeClose(MinerConnection);
                SafeClose(PoolConnection);

                // Upon the next iteration of the while loop in the Run void, the thread will exit and this will be the end.
            }
        }

        /// <summary>
        /// Safe Close
        /// 
        /// Safely closes a socket.
        /// </summary>
        /// <param name="client"></param>
        private void SafeClose(TcpClient client)
        {
            try
            {
                if (client != null) if (client.Connected) client.Close();
            }
            catch (Exception e)
            {
                Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "Failed to close TCP Socket: " + e.ToString());
            }
        }

        /// <summary>
        /// Set Timeouts
        /// 
        /// Sets tiemouts for the sockets.
        /// </summary>
        public void SetTimeouts()
        {
            MinerConnection.SendTimeout = Minutes(2);
            PoolConnection.SendTimeout = Minutes(5);
        }

        /// <summary>
        /// Time Multiplier - Seconds
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public Int32 Seconds(int seconds) => seconds * 1000;

        /// <summary>
        /// Time Multiplier - Minutes
        /// </summary>
        /// <param name="minutes"></param>
        /// <returns></returns>
        public Int32 Minutes(int minutes) => minutes * Seconds(60);

        /// <summary>
        /// Get Proxy Remote Address
        /// 
        /// Returns the remote addresss that the pool sees.
        /// </summary>
        /// <returns></returns>
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
            } else
            {
                return proxyResolvedRemoteAddress;
            }
        }

        /// <summary>
        /// Log Miner Packet
        /// 
        /// Logs the incoming data to the backend.
        /// If it throws an exception, it should disconnect the miner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="data"></param>
        public void LogMinerPacket(string context, string data)
        {
            using (WebClient networkClient = new WebClient())
            {
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("password", configuration.GetPostPassword());
                postParameters.Add("miner_id", id.ToString());
                postParameters.Add("user_address", ((IPEndPoint)MinerConnection.Client.RemoteEndPoint).Address.ToString());
                postParameters.Add("exit_address", GetProxyRemoteAddress());
                postParameters.Add("context", context);
                postParameters.Add("server_name", configuration.GetServerName());
                postParameters.Add("pool_hostname", GetPoolInformationFromMiner().hostname);
                postParameters.Add("data", data);
                networkClient.UploadValues(configuration.GetMinerPacketLoggingEndpoint(), "POST", postParameters);

                Program.ConsoleWriteLineWithColor(ConsoleColor.Green, "Miner packet successfully sent to the backend.");
            }
        }

        /// <summary>
        /// Getter: Get Miner Connection Address
        /// 
        /// Gets the connected address of the miner.
        /// </summary>
        /// <returns></returns>
        public string GetMinerConnectionAddress() => ((IPEndPoint)MinerConnection.Client.RemoteEndPoint).Address.ToString();

        /// <summary>
        /// STRUCT Pool Connection Information
        /// 
        /// A template that specifies an easy to use structure of storing poool data.
        /// </summary>
        public struct PoolConnectionInformation
        {
            public string hostname;
            public int port;
        }

        /// <summary>
        /// GETTER: Gets the poolInformation variable.
        /// </summary>
        /// <returns></returns>
        public PoolConnectionInformation GetPoolInformationFromMiner() => poolInformation;

        /// <summary>
        /// Get Pool Information For Address
        /// 
        /// Get's the pool data for the required address.
        /// </summary>
        /// <returns></returns>
        public PoolConnectionInformation GetPoolInformationForAddress()
        {
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
                    hostname = parsed["hostname"].ToString(),
                    port = int.Parse(parsed["port"].ToString())
                };
            }
        }

        public string LocalLoggerContextBuilder(string context) => String.Format("{0} {1}/{2}", context, id.ToString(), GetPoolInformationFromMiner().hostname); 

        /// <summary>
        /// GETTER: Can be disposed?
        /// </summary>
        /// <returns></returns>
        public bool CanBeDisposed() => dispose;
    }
}