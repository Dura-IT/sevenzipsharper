namespace SevenZipSharper.Interop
{
    internal static class HResult
    {
        internal const int Ok = 0;
        internal const int False = 1;
        internal const int NotImplemented = unchecked((int)0x80004001);
        internal const int NoInterface = unchecked((int)0x80004002);
        internal const int Fail = unchecked((int)0x80004005);
        internal const int Abort = unchecked((int)0x80004004);
        internal const int Pointer = unchecked((int)0x80004003);
        internal const int InvalidArg = unchecked((int)0x80070057);
        internal const int OutOfMemory = unchecked((int)0x8007000E);
    }
}
