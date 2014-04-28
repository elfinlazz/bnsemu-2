namespace NCommons.Network.StsCommands
{
    [CommandData("Auth", "LoginStart", "Request")]
    public class AuthLoginStartRequest : StsCommand<AuthLoginStartRequest>
    {
        [CommandField]
        public string LoginName;
        [CommandField]
        public string NetAddress;
    }

    [CommandData("Auth", "LoginStart", "Reply")]
    public class AuthLoginStartReply : StsCommand<AuthLoginStartReply>
    {
        [CommandField]
        public string KeyData; // this is actually a Base64'd buffer
    }

    [CommandData("Auth", "KeyData", "Request")]
    public class AuthKeyDataRequest : StsCommand<AuthKeyDataRequest>
    {
        [CommandField]
        public string KeyData; // this is actually a Base64'd buffer
    }

    [CommandData("Auth", "KeyData", "Reply")]
    public class AuthKeyDataReply : StsCommand<AuthKeyDataReply>
    {
        [CommandField]
        public string KeyData; // this is actually a Base64'd buffer
    }

    [CommandData("Auth", "KeyData", "Request")]
    public class AuthLoginFinishRequest : StsCommand<AuthLoginFinishRequest>
    {
        [CommandField(Optional = true)]
        public string LongTermSession;
        [CommandField(Optional = true)]
        public string SecondaryAuthToken;
    }

    /*[CommandData("Auth", "LoginFinish", "Reply")]
    public class AuthLoginFinishReply : StsCommand<AuthLoginFinishReply>
    {
        [CommandField]
        public uint AuthType;
        [CommandField(Optional = true)]
        public string LocationId;
        [CommandField(Optional = true)]
        public uint? UserCenter;
        [CommandField(Optional = true)]
        public string UserName;
        [CommandField(Optional = true)]
        public string AccessMask;
        [CommandField(Optional = true)]
        public string Roles; // this is actually another type, probably some array? I think it's a subtree of "RoleId" values.
    }*/
}
