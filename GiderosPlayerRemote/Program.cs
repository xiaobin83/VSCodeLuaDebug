namespace GiderosPlayerRemote
{
    class Program
    {
        static void Main(string[] args)
        {
            var rc = new RemoteController();
            //rc.Run("127.0.0.1", 15000, @"C:\dev\VSCodeLuaDebug\debugee\gideros.gproj");
            rc.Run("127.0.0.1", 15000, @"C:\dev\m\MTCG\MTCG.gproj");
        }
    }
}
