namespace VSCodeDebug
{
    public interface ICDPListener
    {
        void FromVSCode(string command, int seq, dynamic args, string reqText);
    }
}
