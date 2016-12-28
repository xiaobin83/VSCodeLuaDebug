namespace VSCodeDebug
{
    public interface IDebuggeeListener
    {
        void X_FromDebuggee(byte[] json);
        void X_DebugeeHasGone();
    }
}
