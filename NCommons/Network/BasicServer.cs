using NCommons.Utilities;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace NCommons.Network
{
    public abstract class BasicServer<TClient>
        where TClient : BasicClient
    {
        private TcpListener m_listener;

        public BasicServer(ushort port)
        {
            // I am using LoopBack instead of Any because I take the IP from here when sending realm address.
            // temp: using Any to test M$ loopback.
            IPAddress ip = IPAddress.Any;//IPAddress.Loopback;
            m_listener = new TcpListener(ip, port);
        }

        public TcpListener TcpListener
        {
            get { return m_listener; }
        }

        public void Start()
        {
            m_listener.Start();
            m_listener.BeginAcceptTcpClient(HandleAsyncConnection, m_listener);
        }

        private void HandleAsyncConnection(IAsyncResult res)
        {
            m_listener.BeginAcceptTcpClient(HandleAsyncConnection, m_listener);
            TcpClient client = m_listener.EndAcceptTcpClient(res);

            if (!IsClientAccepted(client.Client))
            {
                Log.Warn("Refused connection from {0}", client.Client.RemoteEndPoint);
                return;
            }

            Log.Info("Openned connection from {0}", client.Client.RemoteEndPoint);

            ServerStateObject state = new ServerStateObject();

            state.client = CreateClient(client.Client);

            ClientConnected(state.client);
            client.Client.BeginReceive(state.buffer, 0, ServerStateObject.BufferSize, SocketFlags.None, HandleAsyncReceive, state);
        }

        private void HandleAsyncReceive(IAsyncResult res)
        {
            ServerStateObject state = (ServerStateObject)res.AsyncState;
            TClient client = state.client;

            try
            {
                // read data from the client socket
                int read = client.Socket.EndReceive(res);

                // data was read from client socket
                if (read > 0)
                {
                    using (MemoryStream stream = new MemoryStream(state.buffer, 0, read, false, true))
                        ReceiveMessage(client, stream);

                    // begin receiving again after handling, so we can re-usse the same stateobject/buffer without issues.
                    client.Socket.BeginReceive(state.buffer, 0, ServerStateObject.BufferSize, 0, HandleAsyncReceive, state);
                }
                else
                {
                    // connection was closed.
                    Log.Info("Client closed connection: {0}", client.Socket.RemoteEndPoint);
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    // TODO: More SocketError handling.
                    case SocketError.ConnectionReset:
                        Log.ErrorException(string.Format("Client {0} issued a ConnectionReset error.", client), e);
                        client.Close();
                        break;
                    default:
                        // Swallow the exception, but make sure a log is created and close the client.
                        //Log.ErrorException(string.Format("Unhandled SocketException (Error:{0}) for client {1} while receiving. Closing the client.", e.SocketErrorCode, client.ToString()), e);
                        client.Close();
                        break;
                }
            }
            catch (Exception e)
            {
                // Note: State for this client may have been corrupted at this point, this is 
                //  an important issue when we reach this handler. This will force process to close.

                // Todo: Close anything else if needed (i.e. session, this will catch every unhandled exception in the handler, ect).
                Log.ErrorException("Exception occured while handling a packet", e);

                // We're most likely crashing, client is going to close anyway,
                //  maybe it's better not to do more with it.
                //client.Close();

                throw;
            }
        }

        protected virtual bool IsClientAccepted(Socket clientSocket)
        {
            return true;
        }

        protected abstract TClient CreateClient(Socket socket);
        protected abstract void ClientConnected(TClient client);
        protected abstract void ReceiveMessage(TClient client, MemoryStream packet);

        // See: http://msdn.microsoft.com/en-us/library/5w7b7x5f.aspx
        private class ServerStateObject
        {
            public TClient client;
            // TODO/NOTE: This is the size used by client, should we use another?
            public const int BufferSize = 1024;//0x1FFFE;
            public byte[] buffer = new byte[BufferSize];
        }
    }
}
