namespace SevenZipSharper.Extraction;

// Decides when accumulated writes warrant a flush to physical disk, letting FileEntryStream pace
// decompression to disk without depending on a concrete pacer. See WriteFlushPacer.
internal interface IWriteFlushPacer
{
    // Accumulates bytesWritten and returns true — resetting the counter — once the accumulated
    // total reaches the interval. Always false when the interval is zero or negative (disabled).
    bool ShouldFlush(int bytesWritten);
}
