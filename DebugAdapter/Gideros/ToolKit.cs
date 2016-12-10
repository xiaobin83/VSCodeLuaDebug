// Original work by Hyoung-Kyou Jeon (https://github.com/henjeon/)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace VS2GiderosBridge
{
    /// <summary>
    /// 라이브러리를 사용하려면 이 클래스 인스턴스를 만들고 Start()를 호출한다.
    /// </summary>
    public class ToolKit
    {
        #region members

        System.Text.RegularExpressions.Regex regexLuaSource = new System.Text.RegularExpressions.Regex(@"^\t*(?<path>.*?)\.lua:(?<line>\d+):(?<msg>.*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

        #endregion

        #region property
        /// <summary>
        /// 기데로스 경로. 기본값은 @"C:\Program Files (x86)\Gideros"
        /// </summary>
        public string GiderosPath { get; set; }

        /// <summary>
        /// 기데로스 프로젝트 파일 경로.
        /// 이 파일을 플레이어로 재생한다.
        /// </summary>
        public string GprojPath { get; set; }

        /// <summary>
        /// C# 프로젝트 파일 경로.
        /// 기데로스 프로젝트 파일을 읽어서 이 파일 내용을 채운다.
        /// </summary>
        public string CSharpProjPath { get; set; }
        #endregion

        #region constructors
        public ToolKit()
        {
            GiderosPath = @"C:\Program Files (x86)\Gideros";
        }
        #endregion

        #region start
        /// <summary>
        /// gproj를 열고 csproj를 갱신한다음 플레이어에 전달해서 게임을 실행한다.
        /// </summary>
        public void Start()
        {
            LogWriteLine("");
            LogWriteLine("시작");
            LogWriteLine("");
            LogWriteLine("현재 작업 경로 = {0}", System.Environment.CurrentDirectory);

            if (!System.IO.File.Exists(GprojPath))
            {
                ErrorWriteLine("GprojPath 파일이 없습니다: {0}", GprojPath);
                return;
            }
            if (!System.IO.File.Exists(CSharpProjPath))
            {
                ErrorWriteLine("CSharpProjPath 파일이 없습니다: {0}", CSharpProjPath);
                return;
            }

            /*
            // csharp proj 업데이트
            {
                var gproj2csharpProj = new Gproj2CSharpProj(this);
                gproj2csharpProj.Generate();
            }
            */

            // gdrbridge 기동
            GdrBridge gdrBridge = null;
            try
            {
                gdrBridge = new GdrBridge(this);
            }
            catch
            {
                return;
            }

            gdrBridge.LaunchPlayer();

            // gdrdaemon 부팅을 기다린다.
            {
                const int tryCount = 3;
                int i = 0;
                while (true)
                {
                    if (gdrBridge.IsConnected)
                    {
                        LogWriteLine("daemon 연결 확인함.");
                        break;
                    }

                    if (++i <= tryCount)
                    {
                        LogWriteLine("daemon 연결을 기다리는 중... {0}/{1}", i, tryCount);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        ErrorWriteLine("daemon 연결을 확인하지 못했습니다.");
                        return;
                    }
                }
            }

            // 게임 실행
            gdrBridge.Play();

            // 무한반복
            while (true)
            {
                // 플레이어가 종료되었는지 검사
                if (!gdrBridge.IsPlayerAlive)
                {
                    break;
                }

                // 플레이어 아웃풋을 긁어온다.           
                gdrBridge.GetLog();

                Thread.Sleep(100);
            }
        }
        #endregion

        #region logging
        internal void GiderosWriteLine(string msg)
        {
            try
            {
                var match = regexLuaSource.Match(msg);
                if (match.Success)
                {
                    var location = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GprojPath), match.Groups["path"].Value.Replace('/', '\\')) + ".lua";
                    location = System.IO.Path.GetFullPath(location);
                    var maxCharCount = 60;
                    var blank = location.Length < maxCharCount ? new String(' ', maxCharCount - location.Length) : string.Empty;
                    msg = string.Format("{0}({1}):{2}{3}", location, match.Groups["line"].Value, blank, match.Groups["msg"].Value);
                }
            }
            catch
            {
                // 경로가 |D| 등으로 시작하는 경우의 처리가 아직 없다.                
            }

            Console.WriteLine(msg);
        }
        internal void LogWriteLine(string format, params object[] args)
        {
            Console.WriteLine("[VS2GiderosBridge] " + string.Format(format, args));
        }
        internal void ErrorWriteLine(string format, params object[] args)
        {
            Console.WriteLine("[VS2GiderosBridge] ERROR : " + string.Format(format, args));
        }
        #endregion
    }
}
