using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;
using System.Xml;
using System.IO;
using NCommons.Serialization;

namespace NCommons.Network.StsCommands
{
    public abstract class StsCommand
    {
        protected static string s_protocolName;
        protected static string s_commandName;

        public static string ProtocolName
        {
            get { return s_protocolName; }
        }

        public static string CommandName
        {
            get { return s_commandName; }
        }

        public abstract void WriteTo(XmlWriter writer);
        public abstract void ReadFrom(XmlReader reader);
    }

    public abstract class StsCommand<TCommand> : StsCommand
        where TCommand : StsCommand<TCommand>, new()
    {
        private static Action<XmlWriter, TCommand> s_writeDelegate;
        private static Action<XmlReader, TCommand> s_readDelegate;

        static StsCommand()
        {
            var attr = typeof(TCommand).GetCustomAttribute<CommandDataAttribute>();

            if (attr == null)
                throw new ApplicationException("Command doesn't have a CommandDataAttribute.");

            s_commandName = attr.Command;
            s_protocolName = attr.Protocol;
            string headerElementName = attr.RequestTag;

            StsCommandSerialization.GenerateStsCommandDelegates<TCommand>(
                headerElementName,
                out s_writeDelegate,
                out s_readDelegate);

            //s_writeDelegate = Expression.Lambda<Action<XmlWriter, TCommand>>(writerExpression, paramWriter, paramCommand).Compile();
            //s_readDelegate = Expression.Lambda<Action<XmlReader, TCommand>>(readerExpression, paramReader, paramCommand).Compile();
        }

        public sealed override void WriteTo(XmlWriter writer)
        {
            s_writeDelegate(writer, (TCommand)this);
        }

        public sealed override void ReadFrom(XmlReader reader)
        {
            s_readDelegate(reader, (TCommand)this);
        }
    }
}
