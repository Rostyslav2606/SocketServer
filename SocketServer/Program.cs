using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketServer
{
    class Program
    {
        static int port = 8080;
        
        static void Main(string[] args)
        {
            Console.Title = "Server";

            if (args.Length > 0)
            {
                bool success = int.TryParse(args[0], out port);
                if (!success)
                {
                    Console.WriteLine("Incorrect port value set. \nPress any key to exit.");
                    Console.ReadKey();
                    return;
                }
            }
            else
            {
                Console.WriteLine($"Connection port not set.\n The default port is {port}");
            }
            //Run socket server
            AsynchronousSocketServer.StartListening(port);
        }
    }  
}

