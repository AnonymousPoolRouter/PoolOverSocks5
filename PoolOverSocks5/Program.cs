﻿using System;
using System.IO;
using System.Threading;

namespace Router
{
    class Program
    {
        /*
         * Configuration Handler
         * Placeholder variable to hold the configuration handling class
         * The class will handle all passed command line arguments.
         */
        private static ConfigurationHandler configurationHandler;

        /*
         * Relay Handler
         * This is the actual class that will handle the networking magic and relay data along the socks5 proxy.
         */
        private static Server server;

        /*
         * Main Method
         * The main function of where the appllication enters
         */
        static void Main(string[] args)
        {
            Console.WriteLine("Anonymous Pool Routing - Router\n");

            // Initialize a new ConfugrationHandler into the placeholder variable.
            configurationHandler = new ConfigurationHandler();

            // Start the relay.
            server = new Server(configurationHandler);
            server.Work();
        }

        /*
         * Press enter key to exit function
         * Just a simple static function that can be called anywhere to signal that there was an error.
         */
        public static void PressAnyKeyToExit()
        {
            Console.WriteLine("Press enter key to exit.");
            Console.ReadLine();
            Environment.Exit(1);
        }

        public static void LogResponderHandler(string responder, string data)
        {
            // Write
            ConsoleWriteLineWithColorAndTime(ConsoleColor.Yellow, string.Format("{0} Response:", responder));
            Console.WriteLine(data);
        }

        public static void ConsoleWriteLineWithColorAndTime(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(String.Format("[{0}] {1}", DateTime.UtcNow, message));
            Console.ResetColor();
        }

        public static void ConsoleWriteLineWithColor(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
