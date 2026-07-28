namespace SevenZipSharper.Extraction;

// Decides when accumulated writes warrant a flush to physical disk, bounding how far
// decompression can run ahead of OS write-back. A single pacer is shared across every entry of
// one extraction, so the flush cadence bounds the aggregate write backlog — not just the backlog
// within a single large file. Not thread-safe, and does not need to be: 7-Zip opens, writes, and
// closes entry streams strictly one at a time on the extraction thread.
internal sealed class WriteFlushPacer : IWriteFlushPacer
{
    private readonly long _intervalBytes;
    private long _pendingBytes;

    internal WriteFlushPacer(long intervalBytes) => _intervalBytes = intervalBytes;

    // Accumulates bytesWritten and returns true — resetting the counter — once the accumulated
    // total reaches the interval. Always false when the interval is zero or negative (disabled).
    public bool ShouldFlush(long bytesWritten)
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
