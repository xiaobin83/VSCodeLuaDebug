namespace VSCodeDebug
{
    public interface IDebuggeeListener
    {
        void FromDebuggee(byte[] json);
        void DebugeeHasGone();
    }
}
