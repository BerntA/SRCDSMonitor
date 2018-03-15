//=========       Copyright © Bernt Andreas Eide!       ============//
//
// Purpose: Base Source Remote Control Wrapper.
//
//==================================================================//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SRCDSMonitor.RCON
{
    public abstract class SourceRCON
    {
        protected IPAddress _ipAddress;
        protected string _password;
        protected string _port;

        public SourceRCON(string address, string port, string password)
        {
            _password = password;
            _port = port;
            _ipAddress = null;

            // Allow using a DNS server instead of a raw IP only.
            try
            {
                _ipAddress = IPAddress.Parse(address);
            }
            catch
            {
                _ipAddress = Dns.GetHostAddresses(address)[0];
            }
        }

        public abstract string SendCommand(string cmd);
    }
}