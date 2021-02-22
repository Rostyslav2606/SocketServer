using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static SocketServer.SateObject;

namespace SocketServer
{
    class AsynchronousSocketServer
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static readonly ConcurrentDictionary<EndPoint, StateObject> _clientSockets = new ConcurrentDictionary<EndPoint, StateObject>();
        public static Socket _serverSocket;
        public static bool IsListening = true;

        public static void StartListening(int Port)
        {
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, Port);
            Console.WriteLine($"Local address and port : {ipPoint.ToString()}");
            
            try
            {
                _serverSocket = new Socket(ipPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _serverSocket.Bind(ipPoint);
                _serverSocket.Listen(10);
                while (IsListening)
                {
                    allDone.Reset();
                    Console.WriteLine("\r\nWaiting for a connection...");
                    _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), _serverSocket);
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error message - {e.Message}");
                IsListening = false;
                foreach (var itemSocket in _clientSockets)
                {
                    DisconnectSoket(itemSocket.Value);
                }
            }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            handler.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            Console.WriteLine($"Client IP - {handler.RemoteEndPoint}  connected.");
            IsListening = true;
            // Signal the main thread to continue. 
            allDone.Set();
            Send(handler, $"Server -> HI {handler.RemoteEndPoint}!!!\r\nSet command>");

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            _clientSockets.TryAdd(handler.RemoteEndPoint, state);
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            // Retrieve the state object and the handler socket from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            try
            {
                int bytesRead = 0;
                try
                {
                    bytesRead = handler.EndReceive(ar);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error message - {e.Message}");
                    handler.Close();               
                }

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    var part = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    state.sb.Append(part);
                    // Check for end-of-file tag. If it is not there, read more data.  
                    int CountIndexOf = content.IndexOf("\r\n");
                    if (part.Contains("\r\n"))
                    {
                        content = state.sb.ToString().ToLower();
                        state.sb.Clear();

                        // It is possible to get more then one command
                        var commands = content.Split("\r\n");
                        var commandsCount = commands.Length - 1;

                        if(commands[commandsCount] != string.Empty)
                        {
                            // Last command is not complete
                            state.sb.Append(commands[commandsCount]);
                        }

                        for(int i = 0; i < commandsCount; i++)
                            ProcessCommand(state, commands[i]);

                        if(handler.Connected)
                        {
                            Send(handler, $"Set command>");
                            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                        }
                    }
                    else
                    {
                        // Not all data received. Get more.  
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                    }
                }
                else
                {
                    // Connection closed
                    DisconnectSoket(state);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
                DisconnectSoket(state); // Dont shutdown because the socket may be disposed and its disconnected anyway
            }
        }

        private static void ProcessCommand(StateObject state, string content)
        {
            if (string.IsNullOrEmpty(content))
                return;

            var handler = state.workSocket;

            if (int.TryParse(content, out var tempIn))
            {
                //Set integer
                state.Sum += tempIn;
                Console.WriteLine($"Integer entered to {tempIn}.\r\nThe total amount of entered integers is = {state.Sum}\r\n");
                Send(handler, $"Total  integers - {state.Sum}\r\n");
                state.sb.Clear();
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            else if (content == "list")
            {
                // Get all clients and summa  
                foreach (var itemSocket in _clientSockets)
                {
                    Send(handler, $"Key - {itemSocket.Key}, Sum - {itemSocket.Value.Sum}\r\n");
                }

            }
            else if (content.IndexOf("exit") > -1)
            {
                // Disconnect a client from a server.
                DisconnectSoket(state);
            }
            else if (content.IndexOf("exitall") > -1)
            {
                // Disconnect all clients from a server.
                IsListening = false;
                foreach (var itemSocket in _clientSockets)
                {
                    DisconnectSoket(itemSocket.Value);
                }
            }
            else
            {
                // The set data is not correct. Get more.
                Send(handler, $"You entered an invalid integer. \r\nEnter an integer or one of the commands.\r\n\r\n");
            }
        }

        #region SendMessage
        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        #endregion

        //Сlosing a socket and deleting a StateOject from the collection ConcurrentDictionary
        private static void DisconnectSoket(StateObject stateObject)
        {
            if (_clientSockets.TryRemove(stateObject.workSocket.RemoteEndPoint, out stateObject))
            {
                Console.WriteLine($"Client {stateObject.workSocket.RemoteEndPoint}  disconnected");
                stateObject.workSocket.Shutdown(SocketShutdown.Both);         
                stateObject.workSocket.Close();           
            }
        }
    }
}
