// Original work by Hyoung-Kyou Jeon (https://github.com/henjeon/)

using System;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace VS2GiderosBridge
{
    /// <summary>
    /// gdrbridge.exe 래퍼
    /// </summary>
    class GdrBridge
    {
        public ToolKit ToolKit { get; private set; }
        public string ExecPath { get; private set; }
        public bool IsPlayerAlive
        {
            get
            {
                if (player != null)
                {
                    return !player.HasExited;
                }

                return false;
            }
        }

        Process player;

        /// <summary>
        /// 기데로스 플레이어와 데몬이 연결되어 있는지 확인.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                string output = string.Empty;

                try
                {
                    var psi = new ProcessStartInfo();
                    psi.FileName = ExecPath;
                    psi.Arguments = string.Format("isconnected");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    var p = Process.Start(psi);

                    var stream = p.StandardOutput;
                    output = stream.ReadToEnd();

                    p.WaitForExit();
                }
                catch
                {
                }

                return !string.IsNullOrEmpty(output) && output != "0";
            }
        }

        #region constructors
        public GdrBridge(ToolKit toolkit)
        {
            ToolKit = toolkit;

            ExecPath = Path.Combine(ToolKit.GiderosPath, @"Tools\gdrbridge.exe");
            if (!File.Exists(ExecPath))
            {
                toolkit.ErrorWriteLine("{0} 파일이 없습니다.", ExecPath);
                throw new Exception();
            }
        }
        #endregion

        #region commands


        public void LaunchPlayer()
        {
            if (player != null)
            {
                return;
            }

            string path = Path.Combine(ToolKit.GiderosPath, @"GiderosPlayer.exe");
            try
            {
                ToolKit.LogWriteLine("\"{0}\" 실행...", path);

                var psi = new ProcessStartInfo();
                psi.FileName = path;
                player = Process.Start(psi);
            }
            catch
            {
            }
        }

        public void Play()
        {
            Play(ToolKit.GprojPath);
        }

        public void Play(string gprojPath)
        {
            try
            {
                ToolKit.LogWriteLine("\"{0}\" 실행...", gprojPath);

                var psi = new ProcessStartInfo();
                psi.FileName = ExecPath;
                psi.Arguments = string.Format("play \"{0}\"", gprojPath);
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();
            }
            catch
            {
            }
        }

        public void Stop()
        {
            try
            {
                ToolKit.LogWriteLine("게임을 종료...");

                var psi = new ProcessStartInfo();
                psi.FileName = ExecPath;
                psi.Arguments = string.Format("stop");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();
            }
            catch
            {
            }
        }

        public void StopDeamon()
        {
            try
            {
                ToolKit.LogWriteLine("deamon을 종료...");

                var psi = new ProcessStartInfo();
                psi.FileName = ExecPath;
                psi.Arguments = string.Format("stopdeamon");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();
            }
            catch
            {
            }
        }

        public void GetLog(bool redirect = true)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = ExecPath;
            psi.Arguments = string.Format("getlog");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            if (redirect)
            {
                psi.RedirectStandardOutput = redirect;
                psi.StandardOutputEncoding = Encoding.UTF8;
            }
            var p = Process.Start(psi);

            if (redirect)
            {
                var stream = p.StandardOutput;
                while (true)
                {
                    string line = stream.ReadLine();
                    if (line != null)
                    {
                        ToolKit.GiderosWriteLine(line);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            p.WaitForExit();
        }

        #endregion
    }
}
