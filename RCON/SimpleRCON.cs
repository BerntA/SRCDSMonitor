//=========       Copyright © Bernt Andreas Eide!       ============//
//
// Purpose: Simple RCON Handler - SendCommand sends one cmd at a time, returns the output from the server.
//
//==================================================================//

using QueryMaster;
using QueryMaster.GameServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SRCDSMonitor.RCON
{
    public class SimpleRCON : SourceRCON
    {
        public SimpleRCON(string address, string port, string password)
            : base(address, port, password)
        {
        }

        public override string SendCommand(string cmd)
        {
            try
            {
                string reply = null;
                using (var server = ServerQuery.GetServerInstance(EngineType.Source, new IPEndPoint(_ipAddress, int.Parse(_port))))
                {
                    if (server.GetControl(_password))
                    {
                        reply = server.Rcon.SendCommand(cmd);
                        server.Rcon.Dispose();
                    }
                }

                return reply;
            }
            catch
            {
                return null;
            }
        }
    }
}