//=========       Copyright © Bernt Andreas Eide!       ============//
//
// Purpose: Main App Entry Point + logic.
//
//==================================================================//

using SRCDSMonitor.RCON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SRCDSMonitor
{
    public static class Program
    {
        public static bool ShouldShutdown() { return _shouldShutdown; }

        public static Dictionary<string, string> _data = null;
        public static string _port = "27015";
        public static string _address = null;
        public static string _rconPassword = null;
        private static bool _shouldShutdown = false;
        private static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            // Locate the 'startup' script:
            if (!LoadStartupScript())
            {
                Console.WriteLine("Unable to load server_config.txt, file does not exist or is invalid!\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            _address = Utils.GetLocalIPAddress();
            if (string.IsNullOrEmpty(_address))
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (_data.ContainsKey("+port"))
                _port = _data["+port"];

            _rconPassword = Utils.GetRandomString(12);
            _shouldShutdown = false;

            SimpleRCON rconBase = new SimpleRCON(_address, _port, _rconPassword);

            Console.WriteLine(string.Format("SRCDS monitor has been initialized for {0}, using RCON password {1}:\n", _data["game"], _rconPassword));
            Console.WriteLine("Commands:\nRestart\nReloadscript\nShowdata\nQuit\n\nAnything else will be written directly to the server via RCON.\n");
            Console.ForegroundColor = ConsoleColor.White;

            Thread.Sleep(300);
            GameServer gameServerThread = new GameServer(_address, _port, _rconPassword, _data);
            Thread gameServerMonitorThread = new Thread(new ThreadStart(gameServerThread.MonitorGameServer));
            gameServerMonitorThread.Start();

            while (true)
            {
                string line = Console.ReadLine();
                string cmd = line.ToLower();
                if (cmd.Equals("quit"))
                {
                    _shouldShutdown = true;
                    Console.WriteLine("Quitting, server shutting down!");
                    Thread.Sleep(250);
                    break;
                }
                else if (cmd.Equals("restart"))
                {
                    gameServerThread.StopGameServer(false);
                    Thread.Sleep(250);
                }
                else if (cmd.Equals("showdata"))
                {
                    foreach (KeyValuePair<string, string> pair in _data.ToArray())
                        Console.WriteLine(string.Format("{0} : {1}", pair.Key, pair.Value));
                }
                else if (cmd.Equals("reloadscript"))
                {
                    _data.Clear();
                    if (!LoadStartupScript())
                    {
                        _shouldShutdown = true;
                        Console.WriteLine("Unable to load server_config.txt, file does not exist or is invalid!\nPress any key to exit...");
                        Thread.Sleep(250);
                        Console.ReadKey();
                        break;
                    }
                    Console.WriteLine("Reloaded server_config.txt successfully, you have to restart the server in order for the changes to take effect!");
                    gameServerThread.SetData(_data);
                }
                else // Send via source RCON.
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(rconBase.SendCommand(line));
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            gameServerThread.StopGameServer(false);
            rconBase = null;
            _data.Clear();
            _data = null;
            Thread.Sleep(250);
        }

        private static bool LoadStartupScript()
        {
            StreamReader reader = null;
            try
            {
                reader = new StreamReader(string.Format("{0}\\server_config.txt", Environment.CurrentDirectory));
                _data = new Dictionary<string, string>();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Replace(" ", "").StartsWith("//"))
                        continue;

                    string[] kvs = line.Split('\t');
                    if (kvs.Length > 2)
                        continue;

                    if (_data.ContainsKey(kvs[0]))
                        continue;

                    _data.Add(kvs[0], kvs[1]);
                }

                return (_data.Count() > 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                    reader = null;
                }
            }
        }
    }
}
