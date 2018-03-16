using Newtonsoft.Json.Linq;
using Router.Socket;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;

namespace Router
{
    class BackendConnector
    {
        public static void LogMinerPacket(ConfigurationHandler configuration, Miner miner, string context, string data)
        {
            using (WebClient networkClient = new WebClient())
            {
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("password", configuration.GetPostPassword());
                postParameters.Add("miner_id", miner.id.ToString());
                postParameters.Add("user_address", ((IPEndPoint)miner.MinerConnection.Client.RemoteEndPoint).Address.ToString());
                postParameters.Add("exit_address", miner.GetProxyRemoteAddress());
                postParameters.Add("context", context);
                postParameters.Add("server_name", configuration.GetServerName());
                postParameters.Add("pool_hostname", miner.GetPoolInformationFromMiner().hostname);
                postParameters.Add("data", data);

                networkClient.UploadValues(configuration.GetMinerPacketLoggingEndpoint(), "POST", postParameters);
                Program.ConsoleWriteLineWithColor(ConsoleColor.Green, DateTime.UtcNow + " - Miner packet posted to the backend.");
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

                networkClient.UploadValues(configuration.GetServerPacketLoggingEndpoint(), "POST", postParameters);
                Program.ConsoleWriteLineWithColor(ConsoleColor.Green, DateTime.UtcNow + " - Server has posted statistics to the backend.");

            }
        }

        public static Program.PoolConnectionInformation GetInformation(ConfigurationHandler configuration, Miner miner)
        {
            using (WebClient networkClient = new WebClient())
            {
                string address = miner.GetMinerConnectionAddress();
                NameValueCollection postParameters = new NameValueCollection();
                postParameters.Add("password", configuration.GetPostPassword());
                postParameters.Add("address", address);
                JObject parsed =  JObject.Parse(Encoding.UTF8.GetString(networkClient.UploadValues(configuration.GetPoolInformatonEndpoint(), "POST", postParameters)));
                return new Program.PoolConnectionInformation
                {
                    hostname = parsed["hostname"].ToString(),
                    port = int.Parse(parsed["port"].ToString())
                };
            }
        }
    }
}
