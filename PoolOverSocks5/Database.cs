using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Router
{
    class Database
    {
        // Inherited Configuration
        private ConfigurationHandler configuration;

        // MySQL Related Connection Variables
        private MySqlConnection connection;

        // Structs
        public struct MinerConnectionInformation
        {
            public string PoolAddress;
            public int PoolPort;
        }

        public Database(ConfigurationHandler configuration)
        {
            this.configuration = configuration;
        }

        private MySqlConnection SafeMySQLConnectionCreator()
        {
            while (connection == null || connection.State != ConnectionState.Open)
            {
                Program.ConsoleWriteLineWithColor(ConsoleColor.Yellow, "Attempting to connect to the MySQL Server...");
                try
                {
                    // Create a new connection instance and try to connect
                    connection = new MySqlConnection(GenerateConnectionString());
                    connection.Open();

                    // Log that we were successful.
                    Program.ConsoleWriteLineWithColor(ConsoleColor.Green, "Successfully connected to the MySQL Server!");

                }
                catch (Exception e)
                {
                    // Failure, retry in 30 seconds.
                    Program.ConsoleWriteLineWithColor(ConsoleColor.Red, "Failed to connect to the MySQL Server - Retrying in 30 seconds.");
                    Thread.Sleep(30 * 3000);
                }
            }

            return connection;
        }

        public void SafeMySQLClose()
        {
            if (connection.State == ConnectionState.Open) connection.Close();
        }

        private string GenerateConnectionString()
        {
            return String.Format(
                "server={0};user={3};database={2};port={1};password={4}",
                configuration.GetMySQLHostname(),
                configuration.GetMySQLPort(),
                configuration.GetMySQLDatabase(),
                configuration.GetMySQLUsername(),
                configuration.GetMySQLPassword()
                );
        }

        public MinerConnectionInformation GetMinerAddressInformation(TcpClient MinerConnection)
        {
            // Get the miner address.
            string address = ((IPEndPoint)MinerConnection.Client.RemoteEndPoint).Address.ToString();

            // Build the query
            string query = string.Format("SELECT pool_address, pool_port WHERE user_address = '{0}'", address);

            // Make sure we have a connection open and execute the command.
            MySqlCommand executedQuery = new MySqlCommand(query, SafeMySQLConnectionCreator());
            MySqlDataReader reader = executedQuery.ExecuteReader();

            return new MinerConnectionInformation
            {
                PoolAddress = reader.GetString("pool_address"),
                PoolPort = reader.GetInt16("pool_port")
            };
        }
    }
}
