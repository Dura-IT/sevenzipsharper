namespace SevenZipSharper.Extraction;

// Decides when accumulated writes to a single output file warrant a flush to physical disk,
// bounding how far decompression can run ahead of OS write-back. Not thread-safe: each
// FileEntryStream is written sequentially by one 7-Zip decompression callback.
internal sealed class WriteFlushPacer
{
    private readonly long _intervalBytes;
    private long _pendingBytes;

    internal WriteFlushPacer(long intervalBytes) => _intervalBytes = intervalBytes;

    // Accumulates bytesWritten and returns true — resetting the counter — once the accumulated
    // total reaches the interval. Always false when the interval is zero or negative (disabled).
    internal bool ShouldFlush(int bytesWritten)
    {
        if (_intervalBytes <= 0)
            return false;

        _pendingBytes += bytesWritten;
        if (_pendingBytes < _intervalBytes)
            return false;

        _pendingBytes = 0;
        return true;
    }
}
