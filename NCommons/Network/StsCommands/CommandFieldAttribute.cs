using System;

namespace NCommons.Network.StsCommands
{
    [AttributeUsage(AttributeTargets.Field)]
    class CommandFieldAttribute : Attribute
    {
        private bool m_optional = false;

        public bool Optional
        {
            get { return m_optional; }
            set { m_optional = value; }
        }

        public CommandFieldAttribute() { }
    }
}
