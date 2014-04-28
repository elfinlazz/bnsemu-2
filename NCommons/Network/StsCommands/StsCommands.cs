using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NCommons.Network.StsCommands
{
    [CommandData("Sts", "Connect", "Connect")]
    public class StsConnect : StsCommand<StsConnect>
    {
        [CommandField]
        public uint ConnType;
        [CommandField(Optional = true)]
        public uint? ConnProductType; // optional
        [CommandField(Optional = true)]
        public uint? ConnAppIndex; // optional
        [CommandField(Optional = true)]
        public uint? ConnDeployment; // optional
        [CommandField(Optional = true)]
        public uint? ConnEpoch; // optional
        [CommandField]
        public string Address;
        [CommandField]
        public uint ProductType;
        [CommandField]
        public uint AppIndex;
        [CommandField(Optional = true)]
        public uint? Deployment; // optional
        [CommandField]
        public uint Epoch;
        [CommandField]
        public uint Program;
        [CommandField]
        public uint Build;
        [CommandField]
        public uint Process;
        [CommandField(Optional = true)]
        public uint? NotifyFlags; // optional
        [CommandField(Optional = true)]
        public uint? VersionFlags; // optional
    }
}
