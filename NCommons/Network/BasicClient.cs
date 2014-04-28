using NCommons.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NCommons.Network
{
    public abstract class BasicClient
    {
        private Socket m_socket;

        public Socket Socket
        {
            get { return m_socket; }
            protected set { m_socket = value; }
        }

        public BasicClient(Socket socket)
        {
            Socket = socket;
        }

        public void Close()
        {
            Socket.Close(0);
            // TODO: we may want to code a close "notice" dispatcher. Some servers may
            //  want to be notified as soon as the client is closed, to remove a unit from world, by example.
        }

        public void SendMessage(MemoryStream stream)
        {
            // we're using the stream's buffer, is this thread-safe for whatever may be re-using the buffer after?
            byte[] buffer = stream.GetBuffer();
            Socket.BeginSend(buffer, 0, (int)stream.Length, SocketFlags.None, new AsyncCallback(HandleAsyncSend), this);
        }

        private static void HandleAsyncSend(IAsyncResult res)
        {
            BasicClient client = (BasicClient)res.AsyncState;

            try
            {
                int bytesSent = client.Socket.EndSend(res);

                //Logs.Log(LogType.Network, "Sent {0} bytes to client {1}", bytesSent, client.Socket.RemoteEndPoint);
            }
            catch (SocketException e)
            {
                // Swallow the exception, but make sure a log is created and close the client.
                Log.ErrorException("Unhandled SocketException", e);
                client.Close();
            }
            catch (Exception e)
            {
                Log.ErrorException("Failed to send message to a client. Exception", e);

                // don't swallow, this is a bug.
                throw;
            }
        }

        public override string ToString()
        {
            if (Socket == null)
                return "Invalid BaseClient";

            return Socket.RemoteEndPoint.ToString();
        }
    }
}
