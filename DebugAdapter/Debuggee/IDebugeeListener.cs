namespace VSCodeDebug
{
    public interface IDebugeeListener
    {
        void FromDebuggee(byte[] json);
        void DebugeeHasGone();
    }
}
