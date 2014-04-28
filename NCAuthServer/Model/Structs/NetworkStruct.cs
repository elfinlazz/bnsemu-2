namespace NCAuthServer.Model.Structs
{
    public struct NetworkStruct
    {
        public string PublicIp;

        public ushort PublicPort;

        public NetworkStruct(string ip1, ushort port1)
        {
            PublicIp = ip1;
            PublicPort = port1;
        }
    }
}
