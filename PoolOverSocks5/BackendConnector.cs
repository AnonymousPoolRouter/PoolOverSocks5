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
