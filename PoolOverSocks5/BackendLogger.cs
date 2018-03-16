using Router.Socket;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;

namespace Router
{
    class BackendLogger
    {
        public static void LogMinerPacket(ConfigurationHandler configuration, Miner miner, string context, string data)
        {
            using (WebClient networkClient = new WebClient())
            {
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("oassword", configuration.GetPostPassword());
                postParameters.Add("miner_id", miner.id.ToString());
                postParameters.Add("user_address", ((IPEndPoint)miner.MinerConnection.Client.RemoteEndPoint).Address.ToString());
                postParameters.Add("exit_address", miner.GetProxyRemoteAddress());
                postParameters.Add("context", context);
                postParameters.Add("data", data);

                while (true)
                {
                    try
                    {
                        networkClient.UploadValues(configuration.GetMinerPacketLoggingEndpoint(), "POST", postParameters);
                        return;
                    } catch (Exception e)
                    {
                        Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "Failed to upload miner packet to the backend - Retrying in 5 seconds.");
                        Console.WriteLine(e.ToString());
                        Thread.Sleep(5000);
                    }
                }
            }
        }

        public static void LogConnectionCount(ConfigurationHandler configuration, Server server)
        {
            using (WebClient networkClient = new WebClient())
            {
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("oassword", configuration.GetPostPassword());
                postParameters.Add("server_name", configuration.GetServerName());
                postParameters.Add("server_broadcast_hostname", configuration.GetServerBroadcast());
                postParameters.Add("connections", server.GetMinerCount().ToString());

                while (true)
                {
                    try
                    {
                        networkClient.UploadValues(configuration.GetServerPacketLoggingEndpoint(), "POST", postParameters);
                        Program.ConsoleWriteLineWithColor(ConsoleColor.Green, DateTime.UtcNow + " - Server has posted statistics to the backend.");
                        return;
                    }
                    catch (Exception e)
                    {
                        Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "Failed to upload server packet to the backend - Retrying in 5 seconds.");
                        Console.WriteLine(e.ToString());
                        Thread.Sleep(5000);
                    }
                }
            }
        }
    }
}
