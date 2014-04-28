using NCommons.Cryptography;
using NCommons.Network;
using NCommons.Network.StsCommands;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace NCAuthServer.Network.Sts
{
    class StsClient : BasicClient
    {
        private RC4Crypt m_cryptIn;
        private RC4Crypt m_cryptOut;
        private string m_pendingCommand;
        private int m_lastRequestId;
        private SRP6 m_srp;
        private StsClientStatus m_currentStatus;

        public StsClient(Socket socket)
            : base(socket) { }

        public RC4Crypt CryptIn
        {
            get { return m_cryptIn; }
            set { m_cryptIn = value; }
        }

        public RC4Crypt CryptOut
        {
            get { return m_cryptOut; }
            set { m_cryptOut = value; }
        }

        /// <summary>
        /// Gets or sets the complete command name that is currently awaiting for a body packet.
        /// </summary>
        public string PendingCommand
        {
            get { return m_pendingCommand; }
            set { m_pendingCommand = value; }
        }

        public int LastRequestId
        {
            get { return m_lastRequestId; }
            set { m_lastRequestId = value; }
        }

        public SRP6 Srp
        {
            get { return m_srp; }
            set { m_srp = value; }
        }

        public StsClientStatus CurrentStatus
        {
            get { return m_currentStatus; }
            set { m_currentStatus = value; }
        }

        public void SendOkReplyStream(MemoryStream dataStream)
        {
            using (MemoryStream packetStream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(packetStream))
            {
                // write headers.
                writer.WriteLine("STS/1.0 200 OK");
                writer.WriteLine("l:{0}", dataStream.Length + 1);
                writer.WriteLine("s:{0}R", LastRequestId);
                writer.WriteLine();
                writer.Flush();

                // copy xml data to stream.
                dataStream.WriteTo(packetStream);

                // HTTP content ends with a single \n
                writer.Write('\n');
                writer.Flush();

                // apply encryption if active.
                if (CryptOut != null)
                    CryptOut.EncryptBuffer(packetStream.GetBuffer(), 0, packetStream.Length);

                // test output
                //Logs.Log(Encoding.UTF8.GetString(packetStream.GetBuffer(), 0, (int)packetStream.Length));

                SendMessage(packetStream);
            }
        }

        public void SendOkReply(StsCommand cmd)
        {
            /* With the current implementation it is necessary to make two streams if we want the xml data length.
             *  (the client absolutely requires that the 'l' and 's' headers be exact)
             */

            // todo: make this static?
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.Encoding = Encoding.UTF8;
            settings.OmitXmlDeclaration = true;

            using (MemoryStream dataStream = new MemoryStream())
            using (XmlWriter xmlWriter = XmlWriter.Create(dataStream, settings))
            {
                // serialize the command to xml.
                cmd.WriteTo(xmlWriter);
                xmlWriter.Flush();

                using (MemoryStream packetStream = new MemoryStream())
                using (StreamWriter writer = new StreamWriter(packetStream))
                {
                    // write headers.
                    writer.WriteLine("STS/1.0 200 OK");
                    writer.WriteLine("l:{0}", dataStream.Length + 1);
                    writer.WriteLine("s:{0}R", LastRequestId);
                    writer.WriteLine();
                    writer.Flush();

                    // copy xml data to stream.
                    dataStream.WriteTo(packetStream);

                    // HTTP content ends with a single \n
                    writer.Write('\n');
                    writer.Flush();

                    // apply encryption if active.
                    if (CryptOut != null)
                        CryptOut.EncryptBuffer(packetStream.GetBuffer(), 0, packetStream.Length);

                    // test output
                    //Logs.Log(Encoding.UTF8.GetString(packetStream.GetBuffer(), 0, (int)packetStream.Length));

                    SendMessage(packetStream);
                }
            }
        }

        public void SendOkReply()
        {
            /* With the current implementation it is necessary to make two streams if we want the xml data length.
             *  (the client absolutely requires that the 'l' and 's' headers be exact)
             */

            // todo: make this static?
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.Encoding = Encoding.UTF8;
            settings.OmitXmlDeclaration = true;

            using (MemoryStream dataStream = new MemoryStream())
            using (XmlWriter xmlWriter = XmlWriter.Create(dataStream, settings))
            {
                using (MemoryStream packetStream = new MemoryStream())
                using (StreamWriter writer = new StreamWriter(packetStream))
                {
                    // write headers.
                    writer.WriteLine("STS/1.0 200 OK");
                    //writer.WriteLine("l:{0}", dataStream.Length + 1);
                    //writer.WriteLine("s:{0}R", LastRequestId);
                    writer.WriteLine();
                    writer.Flush();

                    SendMessage(packetStream);
                }
            }
        }

        public void SendErrorReply(string errorType, string errorData)
        {
            using (MemoryStream packet = new MemoryStream())
            using (TextWriter writer = new StreamWriter(packet))
            {
                writer.WriteLine("STS/1.0 400 {0}", errorType);
                writer.WriteLine("l:{0}", errorData.Length + 1);
                writer.WriteLine("s:{0}", LastRequestId); // we may change this system eventually.
                writer.WriteLine();
                writer.Write(errorData);
                writer.Write('\n');

                writer.Flush();
                this.SendMessage(packet);
            }
        }
    }

    enum StsClientStatus
    {
        /// <summary>
        /// The TCP connection is opened but nothing has happened yet.
        /// </summary>
        None = 0,
        /// <summary>
        /// The client is connected via the StsConnect command.
        /// </summary>
        Connected,
        LoginStart,
        ReceivedKeyData, // packets are encrypted after first crossing this point.
        LoginFinish,
    }
}
