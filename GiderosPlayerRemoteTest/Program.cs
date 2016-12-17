using System;

namespace GiderosPlayerRemote
{
    class Program
    {
        static void Log(LogType logType, string text)
        {
            Console.WriteLine(logType + " " + text);
        }

        static void Main(string[] args)
        {
            var rc = new RemoteController();
            //rc.Run("127.0.0.1", 15000, @"C:\dev\VSCodeLuaDebug\debugee\gideros.gproj");
            if (rc.TryStart("127.0.0.1", 15000, @"C:\dev\MTCG\MTCG.gproj", Log))
            {
                rc.ReadLoop();
            }
            else
            {
                Console.WriteLine("connection failed");
            }
        }
    }
}
