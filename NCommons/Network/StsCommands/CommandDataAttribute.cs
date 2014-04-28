using System;

namespace NCommons.Network.StsCommands
{
    [AttributeUsage(AttributeTargets.Class)]
    class CommandDataAttribute : Attribute
    {
        public string RequestTag { get; set; }
        public string Protocol { get; set; }
        public string Command { get; set; }

        public CommandDataAttribute(string protocol, string command)
        {
            Protocol = protocol;
            Command = command;
        }
        public CommandDataAttribute(string protocol, string command, string requestTag)
            : this(protocol, command)
        {
            RequestTag = requestTag;
        }
    }
}
