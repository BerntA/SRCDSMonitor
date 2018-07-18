//=========       Copyright © Bernt Andreas Eide!       ============//
//
// Purpose: Static utilities.
//
//==================================================================//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SRCDSMonitor
{
    public static class Utils
    {
        public static string GetRandomString(int size)
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            string input = "abcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder builder = new StringBuilder();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = input[random.Next(0, input.Length)];
                builder.Append(ch);
            }
            return builder.ToString();
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }

                Console.WriteLine("No local IPv4 address found!");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public static string GetStringForList(List<string> input)
        {
            if (input.Count() > 0)
            {
                StringBuilder bldr = new StringBuilder();
                foreach (string s in input)
                    bldr.Append(string.Format("{0} ", s));

                return bldr.ToString();
            }

            return string.Empty;
        }
    }
}
