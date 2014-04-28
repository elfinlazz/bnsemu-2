using NCAuthServer.Database;
using NCAuthServer.Model.Account;
using NCommons.Cryptography;
using NCommons.Network;
using NCommons.Network.StsCommands;
using NCommons.Utilities;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace NCAuthServer.Network.Sts
{
    sealed class StsServer : BasicServer<StsClient>
    {
        public StsServer(ushort port)
            : base(port) { }

        protected override StsClient CreateClient(Socket socket)
        {
            return new StsClient(socket);
        }

        protected override void ClientConnected(StsClient client)
        {
            Log.Info("New client connected.");
        }

        protected override void ReceiveMessage(StsClient client, MemoryStream packet)
        {
            Byte[] buf = packet.GetBuffer();

            if (client.CryptIn != null)
                client.CryptIn.EncryptBuffer(packet.GetBuffer(), 0, packet.Length);

            //Log.Debug("ReceiveMessage: {0}", Encoding.UTF8.GetString(packet.GetBuffer()));

            /* The protocol is basically HTTP and respects the rules of HTTP/1.1
             * I'm trying to make this as simple as possible and the code is trying to detect errors.
             *  instead of attempting to handle invalid or corrupted data.
             * I am not absolutely certain of the encoding used.
             */
            using (StreamReader reader = new StreamReader(packet, Encoding.UTF8))
            {
                if (client.PendingCommand == null)
                {
                    while (reader.Peek() > 0)
                    {
                        string[] requestLine = reader.ReadLine().Split(' ');
                        if (requestLine.Length == 3)
                        {
                            string method = requestLine[0];

                            if (method == "POST")
                            {
                                string command = requestLine[1];
                                string type = requestLine[2];
                                Log.Debug("Recieve: {0} {1} {2}", method, command, type);

                                // get headers, there may be more.
                                int l = 0;
                                int s = 0; // "status" index of message

                                string line = reader.ReadLine();
                                while (!String.IsNullOrEmpty(line) && !reader.EndOfStream)
                                {
                                    string[] header = line.Split(new char[] { ':' }, 2);
                                    //Logs.Log("{0} : {1}", header[0], header[1]);

                                    switch (header[0])
                                    {
                                        case "l":
                                            l = int.Parse(header[1]);
                                            break;
                                        case "s":
                                            // NOTE: Could this be more than just a number? Server sends #R.
                                            s = int.Parse(header[1]);
                                            break;
                                        default:
                                            Log.Warn("Unknown header in message: {0}", line);
                                            break;
                                    }

                                    line = reader.ReadLine();
                                }

                                client.LastRequestId = s;

                                char[] buffer = new char[l];
                                reader.Read(buffer, 0, l);
                                StreamReader content = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(buffer)), Encoding.UTF8);
                                DispatchCommand(client, command, content);
                            }
                            else
                            {
                                Log.Warn("StsServer received unhandled request method: {0}", method);
                            }
                        }
                        else
                        {
                            Log.Warn("StsServer received invalid request line: {0}", requestLine);
                        }
                    }
                }
                else
                {
                    DispatchCommand(client, client.PendingCommand, reader);
                    client.PendingCommand = null;
                }
            }


        }

        private void DispatchCommand(StsClient client, string command, StreamReader reader)
        {
            Log.Debug("Dispatching '{0}'", command);

            // NOTE: We maybe could/should use handlers for these, but I don't see any need
            //  for that now.
            switch (command)
            {
                /*case "/Sts/Ping":
                    break;*/
                case "/Sts/Connect":
                    HandleStsConnect(client, reader);
                    break;
                case "/Auth/LoginStart":
                    HandleAuthLoginStart(client, reader);
                    break;
                case "/Auth/KeyData":
                    HandleAuthKeyData(client, reader);
                    break;
                case "/Auth/LoginFinish":
                    HandleAuthLoginFinish(client, reader);
                    break;
                case "/GameAccount/ListMyAccounts":
                    HandleGameAccountListMyAccounts(client, reader);
                    break;
                /*case "/Auth/GetMyUserInfo":
                    HandleAuthGetMyUserInfo(client, reader);
                    break;*/
                default:
                    Log.Warn("{0} is not yet implement", command);
                    Log.Debug(reader.ReadToEnd());
                    break;
            }
        }

        private void HandleStsConnect(StsClient client, StreamReader reader)
        {
            if (client.CurrentStatus != StsClientStatus.None)
            {
                Log.Error("Client {0} sent StsConnect but is already in another state.", client);
                client.Close();
                return;
            }

            StsConnect request = new StsConnect();
            using (XmlReader xmlReader = XmlReader.Create(reader))
                request.ReadFrom(xmlReader);

            Log.Debug("StsConnect: {0} {1} {2}", request.Build, request.Process, request.Address);

            client.CurrentStatus = StsClientStatus.Connected;
        }

        private void HandleAuthLoginStart(StsClient client, StreamReader reader)
        {
            // Test, there's is a possibility that it was already connected but is re-trying.
            /*if (client.CurrentStatus != StsClientStatus.Connected)
            {
                Logs.Log(LogType.Auth, LogLevel.Error, "Client {0} sent AuthLoginStart but is in invalid state.");
                client.Close();
                return;
            }*/

            AuthLoginStartRequest request = new AuthLoginStartRequest();
            using (XmlReader xmlReader = XmlReader.Create(reader))
                request.ReadFrom(xmlReader);

            Log.Info("LoginStart From: {0} / {1}", request.NetAddress, request.LoginName);
            string login = request.LoginName.Split('@')[0];
            var account = AccountMDB.GetInstance().GetAccountByLoginName(login);

            using (MemoryStream keyDataStream = new MemoryStream(4 + 8 + 4 + 128))
            using (BinaryWriter keyDataWriter = new BinaryWriter(keyDataStream))
            {
                try
                {
                    client.Srp = new SRP6();
                    client.Srp.ReceiveLoginStartInfo(request.LoginName, account.Password, keyDataWriter);
                }
                catch (SRP6InvalidStateException ex)
                {
                    // This is an issue we don't want to recover from. Log it and close client connection.
                    Log.ErrorException("", ex);
                    client.Close();
                    return;
                }

                //Byte[] data = keyDataStream.GetBuffer();
                //Logs.Log("Sending first key (len: {0:X2}):", data.Length);
                //Logs.LogBuffer(data);

                AuthLoginStartReply reply = new AuthLoginStartReply();
                reply.KeyData = Convert.ToBase64String(keyDataStream.GetBuffer());
                client.SendOkReply(reply);
            }

            client.CurrentStatus = StsClientStatus.LoginStart;
        }

        private void HandleAuthKeyData(StsClient client, StreamReader reader)
        {
            if (client.CurrentStatus != StsClientStatus.LoginStart)
            {
                Log.Error("Client {0} sent AuthKeyData but is in invalid state.", client);
                return;
            }

            AuthKeyDataRequest request = new AuthKeyDataRequest();
            using (XmlReader xmlReader = XmlReader.Create(reader))
                request.ReadFrom(xmlReader);

            using (MemoryStream clientKeyDataStream = new MemoryStream(Convert.FromBase64String(request.KeyData)))
            using (BinaryReader clientKeyDataReader = new BinaryReader(clientKeyDataStream))
            using (MemoryStream serverKeyDataStream = new MemoryStream(4 + 32))
            using (BinaryWriter serverKeyDataWriter = new BinaryWriter(serverKeyDataStream))
            {
                byte[] key;

                try
                {
                    client.Srp.ReceiveClientProof(clientKeyDataReader, serverKeyDataWriter, out key);
                }
                catch (SRP6InvalidStateException ex)
                {
                    // This is an issue we don't want to recover from. Log it and close client connection.
                    Log.ErrorException(ex.Message, ex);
                    client.Close();
                    return;
                }
                catch (SRP6SafeguardException ex)
                {
                    // This could just mean that the password was wrong, don't even log it.
                    Log.Warn(ex.Message);
                    client.SendErrorReply("ErrBadParam", "<Error code=\"10\"/>");
                    client.CurrentStatus = StsClientStatus.Connected;
                    return;
                }
                finally
                {
                    // Let GC take the mem back.
                    client.Srp = null;
                }

                AuthKeyDataReply reply = new AuthKeyDataReply();
                reply.KeyData = Convert.ToBase64String(serverKeyDataStream.GetBuffer());

                Byte[] data = serverKeyDataStream.GetBuffer();
                Log.Debug("Sending verif key (len: {0:X2}):", data.Length);
                //Logs.LogBuffer(data);

                client.SendOkReply(reply);

                // new RC4: Filter
                //client.Filter = new RC4Filter(key);
                client.CryptIn = new RC4Crypt(key);
                client.CryptOut = new RC4Crypt(key);

                client.CurrentStatus = StsClientStatus.ReceivedKeyData;
            }
        }

        private void HandleAuthLoginFinish(StsClient client, StreamReader reader)
        {
            if (client.CurrentStatus != StsClientStatus.ReceivedKeyData)
            {
                Log.Error("Client {0} sent AuthLoginFinish but is in invalid state.", client);
                return;
            }

            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.AutoFlush = true;
                writer.Write("<Reply>");
                writer.Write("<UserId></UserId>");
                writer.Write("<UserCenter>3</UserCenter>");
                writer.Write("<Roles type=\"array\"/>");
                writer.Write("<LocationId></LocationId>"); // AF228A31-0230-4212-9E55-6E405728B795
                writer.Write("<AccessMask></AccessMask>");
                writer.Write("<UserName>Sandbox</UserName>");
                writer.Write("</Reply>");
                writer.Flush();

                client.SendOkReplyStream(stream);
            }

            client.CurrentStatus = StsClientStatus.LoginFinish;
        }

        private void HandleGameAccountListMyAccounts(StsClient client, StreamReader reader)
        {
            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.AutoFlush = true;
                writer.Write("<Reply type=\"array\">");
                writer.Write("<GameAccount>");
                writer.Write("<Alias></Alias>");
                writer.Write("<Created></Created>");
                writer.Write("</GameAccount>");
                writer.Write("</Reply>");
                writer.Flush();

                client.SendOkReplyStream(stream);
            }
        }

        private void HandleAuthGetMyUserInfo(StsClient client, StreamReader reader)
        {
            // todo
        }
    }
}
