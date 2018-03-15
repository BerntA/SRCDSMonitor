//=========       Copyright © Bernt Andreas Eide!       ============//
//
// Purpose: Game Server Logic Object + monitoring...
//
//==================================================================//

using SRCDSMonitor.RCON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SRCDSMonitor
{
    public class GameServer
    {
        [DllImport("User32")]
        private static extern int ShowWindow(int hwnd, int nCmdShow);
        private const double TIME_UNTIL_EXTENSIVE_CRASH_CHECKING = 120.0;
        private static DateTime lastTimeStarted;
        private static bool serverCrashed = false;

        private string _address;
        private string _port;
        private string _rconPassword;
        private bool _extendedCrashChecking;
        private bool _hideWindow;
        private Dictionary<string, string> _data;
        private SimpleRCON _rconObj;
        private Process _serverProcess;
        public GameServer(string address, string port, string rconpassword, Dictionary<string, string> data)
        {
            _address = address;
            _port = port;
            _rconPassword = rconpassword;
            _data = data;
            _extendedCrashChecking = _data.ContainsKey("extendedcrashchecking") ? _data["extendedcrashchecking"].Equals("1") : false;
            _hideWindow = _data.ContainsKey("hidewindow") ? _data["hidewindow"].Equals("1") : false;
            _rconObj = new SimpleRCON(_address, _port, _rconPassword);
            _serverProcess = null;
        }

        public void SetData(Dictionary<string, string> data)
        {
            _data = data;
        }

        public bool StartGameServer()
        {
            if (Program.ShouldShutdown())
                return false;

            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.AppendFormat("-console -game \"{0}\" +rcon_password \"{1}\" +port {2} ", _data["gameroot"], _rconPassword, _port);

                KeyValuePair<string, string>[] kvs = _data.ToArray();
                for (int i = 5; i < kvs.Count(); i++) // Skip the first 4 base cmds.
                    strBuilder.AppendFormat("{0} {1} ", kvs[i].Key, kvs[i].Value);

                Process serverProcess = new Process();
                serverProcess.EnableRaisingEvents = true;
                serverProcess.Exited += new System.EventHandler(OnGameServerExited);
                serverProcess.StartInfo.UseShellExecute = false;
                serverProcess.StartInfo.ErrorDialog = false;
                serverProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                serverProcess.StartInfo.CreateNoWindow = true;
                serverProcess.StartInfo.FileName = _data["srcds"];
                serverProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(_data["srcds"]);
                serverProcess.StartInfo.Arguments = strBuilder.ToString();
                serverProcess.Start();
                _serverProcess = serverProcess;

                lastTimeStarted = DateTime.Now;
                Thread.Sleep(500);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public void StopGameServer(bool status)
        {
            if (_serverProcess == null)
                return;

            serverCrashed = status;
            _rconObj.SendCommand("disconnect"); // Drop everyone from the server.
            Thread.Sleep(250);
            _rconObj.SendCommand("quit"); // Run a proper clean quit.
            Thread.Sleep(250);
            TerminateProgram(_serverProcess); // If the server still is up, force a kill.
            _serverProcess = null;
        }

        public void MonitorGameServer()
        {
            StartGameServer();

            DateTime currentTime;
            while (!Program.ShouldShutdown())
            {
                Thread.Sleep(3000);

                if (_serverProcess == null)
                    continue;

                currentTime = DateTime.Now;
                TimeSpan timeSinceServerStart = currentTime - lastTimeStarted;

                try
                {
                    // Wait at least 120 sec until checking the remote control status before doing crash checks!
                    // TODO: How to know if server is frozen due to map change? check mp_timelimit cvars ? what about sudden changes by admins etc??
                    if (_extendedCrashChecking && (timeSinceServerStart.TotalSeconds > TIME_UNTIL_EXTENSIVE_CRASH_CHECKING) && _rconObj.SendCommand("") == null)
                        StopGameServer(true);

                    Process[] listOfProcesses = Process.GetProcesses();
                    if (listOfProcesses != null)
                    {
                        for (int i = (listOfProcesses.Count() - 1); i >= 0; i--)
                        {
                            if (listOfProcesses[i].ProcessName.StartsWith("WerFault"))
                            {
                                serverCrashed = true;
                                listOfProcesses[i].Kill();
                            }
                        }
                    }

                    if (_hideWindow && _serverProcess != null && !_serverProcess.HasExited)
                        ShowWindow(_serverProcess.MainWindowHandle.ToInt32(), 0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void TerminateProgram(Process proc)
        {
            try
            {
                proc.Kill();
            }
            catch
            {
            }
        }

        private void OnGameServerExited(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Process proc = ((Process)sender);
                if (proc != null)
                {
                    if (StartGameServer())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        if (serverCrashed)
                            Console.WriteLine(string.Format("({0}) Restarted server due to an unexpected crash or freeze!", DateTime.Now.ToString()));
                        else
                            Console.WriteLine(string.Format("({0}) Restarted server!", DateTime.Now.ToString()));
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
        }
    }
}
