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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SRCDSMonitor
{
    public static class Program
    {
        public static bool ShouldShutdown() { return _shouldShutdown; }

        public static Dictionary<string, string> _data = null;
        public static List<string> _commandLineOptions = null; // Passed to hlds or srcds on server start.
        public static List<string> _crashedCommandLineOptions = null; // Added to default command line options when a crashed server restarts.
        public static string _port = "27015";
        public static string _address = null;
        public static string _rconPassword = null;
        public static GameServer _gameServerThread = null;
        private static bool _shouldShutdown = false;

        #region Trap application termination
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _exitHandler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool HandleExit(CtrlType sig)
        {
            System.Threading.Thread.Sleep(100);
            Shutdown();
            return true;
        }
        #endregion

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

            if (_data.ContainsKey("port"))
                _port = _data["port"];

            _rconPassword = Utils.GetRandomString(12);
            _shouldShutdown = false;

            SimpleRCON rconBase = new SimpleRCON(_address, _port, _rconPassword, _data.ContainsKey("hlds"));

            Console.WriteLine(string.Format("SRCDS monitor has been initialized for {0}, using RCON password {1}:\n", _data["game"], _rconPassword));
            Console.WriteLine("Commands:\nRestart\nReloadscript\nShowdata\nQuit\n\nAnything else will be written directly to the server via RCON.\n");
            Console.ForegroundColor = ConsoleColor.White;

            Thread.Sleep(300);
            _gameServerThread = new GameServer(_address, _port, _rconPassword, _data);
            Thread gameServerMonitorThread = new Thread(new ThreadStart(_gameServerThread.MonitorGameServer));
            gameServerMonitorThread.Start();

            _exitHandler = new EventHandler(HandleExit);
            SetConsoleCtrlHandler(_exitHandler, true);

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
                    _gameServerThread.StopGameServer(false);
                    Thread.Sleep(250);
                }
                else if (cmd.Equals("showdata"))
                {
                    foreach (KeyValuePair<string, string> pair in _data.ToArray())
                        Console.WriteLine(string.Format("{0} : {1}", pair.Key, pair.Value));

                    Console.WriteLine("Command Line:");
                    foreach (string s in _commandLineOptions)
                        Console.WriteLine(s);

                    Console.WriteLine("Passed to command line when restarting from a crash:");
                    foreach (string s in _crashedCommandLineOptions)
                        Console.WriteLine(s);
                }
                else if (cmd.Equals("reloadscript"))
                {
                    _data.Clear();
                    _commandLineOptions.Clear();
                    _crashedCommandLineOptions.Clear();
                    if (!LoadStartupScript())
                    {
                        _shouldShutdown = true;
                        Console.WriteLine("Unable to load server_config.txt, file does not exist or is invalid!\nPress any key to exit...");
                        Thread.Sleep(250);
                        Console.ReadKey();
                        break;
                    }
                    Console.WriteLine("Reloaded server_config.txt successfully, you have to restart the server in order for the changes to take effect!");
                    _gameServerThread.SetData(_data);
                }
                else // Send via source RCON.
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(rconBase.SendCommand(line));
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            rconBase = null;
            Shutdown();
        }

        public static void Shutdown()
        {
            _gameServerThread.StopGameServer(false);

            _data.Clear();
            _commandLineOptions.Clear();
            _crashedCommandLineOptions.Clear();
            _data = null;
            _commandLineOptions = _crashedCommandLineOptions = null;

            Thread.Sleep(250);
            Environment.Exit(-1);
        }

        private static bool IsLineValid(string line, out string k, out string v, out int length)
        {
            k = v = string.Empty;
            length = 0;
            if (string.IsNullOrEmpty(line))
                return false;

            if (line.Replace(" ", "").StartsWith("//"))
                return false;

            string[] kvs = line.Split('\t');
            if (kvs == null || kvs.Length > 2 || kvs.Length <= 0 || string.IsNullOrEmpty(kvs[0]))
                return false;

            if (kvs[0].Contains("+port") || kvs[0].Contains("-port"))
                return false;

            k = kvs[0];
            v = (kvs.Length > 1) ? kvs[1] : string.Empty;
            length = kvs.Length;
            return true;
        }

        private static bool LoadStartupScript()
        {
            try
            {
                _data = new Dictionary<string, string>();
                _commandLineOptions = new List<string>();
                _crashedCommandLineOptions = new List<string>();

                using (StreamReader reader = new StreamReader(string.Format("{0}\\server_config.txt", Environment.CurrentDirectory)))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine(), k, v;
                        int len;
                        if (!IsLineValid(line, out k, out v, out len))
                            continue;

                        // Add command line stuff to a diff data structure:
                        if (k.StartsWith("-") || k.StartsWith("+"))
                        {
                            if (len == 1)
                                _commandLineOptions.Add(k);
                            else
                                _commandLineOptions.Add(string.Format("{0} {1}", k, v));

                            continue;
                        }

                        if (_data.ContainsKey(k))
                            continue;

                        if (len == 1)
                            _data.Add(k, string.Empty);
                        else
                            _data.Add(k, v);
                    }
                }

                using (StreamReader reader = new StreamReader(string.Format("{0}\\server_crashed.txt", Environment.CurrentDirectory)))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine(), k, v;
                        int len;
                        if (!IsLineValid(line, out k, out v, out len))
                            continue;

                        if (len == 1)
                            _crashedCommandLineOptions.Add(k);
                        else
                            _crashedCommandLineOptions.Add(string.Format("{0} {1}", k, v));
                    }
                }

                Console.Title = _data.ContainsKey("game") ? string.Format("{0} - Server", _data["game"]) : "SRCDS Monitor";
                return (_data.Count() > 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            finally
            {
            }
        }
    }
}
