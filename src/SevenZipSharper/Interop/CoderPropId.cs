namespace SevenZipSharper.Interop
{
    // Compression parameter property IDs passed to IOutArchive.SetProperties.
    // Values match NCoderPropID in the 7-Zip SDK (ICoder.h).
    internal enum CoderPropId : uint
    {
        DefaultProp = 0x000,
        DictionarySize = 0x400,
        UsedMemorySize = 0x401,
        Order = 0x402,
        BlockSize = 0x403,
        PosStateBits = 0x404,
        LitContextBits = 0x405,
        LitPosBits = 0x406,
        NumFastBytes = 0x407,
        MatchFinder = 0x408,
        MatchFinderCycles = 0x409,
        NumPasses = 0x40A,
        Algorithm = 0x40B,
        NumThreads = 0x40C,
        EndMarker = 0x40D,
        Level = 0x801,
        NumTrees = 0x802,
        Solid = 0x803,
        BlockSize2 = 0x804,
        Modified = 0x806,
    }
}
