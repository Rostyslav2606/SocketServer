using System.Net.Sockets;
using System.Text;

namespace SocketServer
{
    class SateObject
    {
        #region SateObject
        public class StateObject
        {
            // Size of receive buffer.  
            public const int BufferSize = 1024;
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
            // Client socket.
            public Socket workSocket = null;
            //Total summa
            public int Sum = 0;
        }
        #endregion
    }
}
